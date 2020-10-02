using BOLL7708;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SuperSocket.WebSocket;
using OpenVR2WS.Output;
using static BOLL7708.EasyOpenVRSingleton;

namespace OpenVR2WS
{
    class MainController
    {
        private SuperServer _server = new SuperServer();
        private Properties.Settings _settings = Properties.Settings.Default;
        private EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private StringEnumConverter _converter = new StringEnumConverter();
        private Action<bool> _openvrStatusAction;

        // Data storage


        public MainController(Action<SuperServer.ServerStatus, int> serverStatus, Action<bool> openvrStatus)
        {
            _openvrStatusAction = openvrStatus;
            Data.reset();
            InitServer(serverStatus);

            _vr.SetDebugLogAction((message) => {
                Debug.WriteLine($"Debug log: {message}");
            });
            _vr.Init();

            InitWorkerThread();
        }

        #region WebSocketServer
        private void InitServer(Action<SuperServer.ServerStatus, int> serverStatus)
        {
            _server.StatusAction = serverStatus;
            _server.MessageReceievedAction = (session, message) =>
            {
                var command = new Command();
                try { command = JsonConvert.DeserializeObject<Command>(message); }
                catch (Exception e) { Debug.WriteLine($"JSON Parsing Exception: {e.Message}"); }

                if (command.key != CommandEnum.None) HandleCommand(session, command);
                else _server.SendMessage(session, $"Invalid command: {message}");
            };
            _server.StatusMessageAction = (session, connected, status) =>
            {
                Debug.WriteLine($"Status received: {status}");
                if(connected && _vr.IsInitialized())
                {
                    SendDefaults(session);
                }
            };
            RestartServer(_settings.Port);
        }

        public void RestartServer(int port)
        {
            _server.Start(port);
        }

        // If session is null, it will send to all registered sessions
        private void SendResult(CommandEnum command, Object data=null, WebSocketSession session = null)
        {
            var key = Enum.GetName(typeof(CommandEnum), command);
            SendResult(key, data, session);
        }
        private void SendResult(string key, Object data=null, WebSocketSession session = null)
        {
            var result = new Dictionary<string, dynamic>();
            result["key"] = key;
            result["data"] = data;
            var jsonString = "";
            try
            {
                jsonString = JsonConvert.SerializeObject(result, _converter);
            }
            catch (Exception e) {
                Debug.WriteLine($"Could not serialize output for {key}: {e.Message}");
            }
            if(jsonString != "") _server.SendMessage(session, jsonString);
        }

        private void SendInput(InputDigitalActionData_t data, InputActionInfo info) {
            var source = Data.handleToSource[info.sourceHandle];
            var output = new Dictionary<string, dynamic>() {
                { "source", source },
                { "input", info.pathEnd },
                { "value", data.bState }
            };
            SendResult("Input", output);
        }

        private class Command
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandEnum key = CommandEnum.None;
            public string value = "";
            public string value2 = "";
            public int device = -1;
        }

        enum CommandEnum
        {
            None,
            CumulativeStats,
            PlayArea,
            ApplicationInfo,
            DeviceIds,
            DeviceProperty,
            InputAnalog,
            InputPose,
            Setting
        }

        private void HandleCommand(WebSocketSession session, Command command)
        {
            // Debug.WriteLine($"Command receieved: {Enum.GetName(typeof(CommandEnum), command.key)}");
            if (_stopRunning || !_vr.IsInitialized()) return;
            switch(command.key)
            {
                case CommandEnum.None: break;
                case CommandEnum.CumulativeStats:
                    var stats = _vr.GetCumulativeStats();
                    SendResult(command.key, new CumulativeStats(stats), session);
                    break;
                case CommandEnum.PlayArea: 
                    SendPlayArea(session);
                    break;
                case CommandEnum.ApplicationInfo:
                    SendApplicationInfo(session);
                    break;
                case CommandEnum.DeviceIds:
                    SendDeviceIds(session);
                    break;
                case CommandEnum.DeviceProperty:
                    var property = ETrackedDeviceProperty.Prop_Invalid;
                    try{ property = (ETrackedDeviceProperty) Enum.Parse(typeof(ETrackedDeviceProperty), command.value); } 
                    catch(Exception e) { Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}"); }
                    if (property != ETrackedDeviceProperty.Prop_Invalid)
                    {
                        SendDeviceProperty(command.key, command.device, property, session);
                    }
                    else SendResult("Error", $"Faulty property: {command.value}", session); // TODO: Convert to error func
                    break;
                case CommandEnum.InputAnalog:
                    SendResult(command.key, Data.analogInputActionData, session);
                    break;
                case CommandEnum.InputPose:
                    SendResult(command.key, Data.poseInputActionData, session);
                    break;
                case CommandEnum.Setting:
                    SendSetting(command.key, command.value, command.value2, session);
                    break;
            }
        }
        #endregion

        #region VRWorkerThread
        private Thread _workerThread = null;
        private void InitWorkerThread()
        {
            _workerThread = new Thread(Worker);
            if (!_workerThread.IsAlive) _workerThread.Start();
        }

        private volatile bool _shouldShutDown = false;
        private volatile bool _stopRunning = false;
        private void Worker()
        {
            Thread.CurrentThread.IsBackground = true;
            var initComplete = false;
            var headsetHzUpdated = false;
            var headsetHzMs = 1000/90;

            while(true)
            {
                if(_vr.IsInitialized())
                {
                    Thread.Sleep(headsetHzMs);
                    if (!initComplete)
                    {
                        // Happens once
                        initComplete = true;
                        _stopRunning = false;
                        _vr.LoadAppManifest("./app.vrmanifest");
                        _vr.LoadActionManifest("./actions.json");
                        Data.UpdateDeviceIndices();
                        Data.UpdateInputDeviceHandles();
                        RegisterActions();
                        RegisterEvents();
                        SendDefaults();
                        Debug.WriteLine("Initialization complete!");
                        _openvrStatusAction.Invoke(true);
                    } else
                    {
                        // Happens every loop
                        _vr.UpdateEvents(false);
                        _vr.UpdateActionStates(new[] {
                            Data.sourceToHandle[InputSource.LeftHand], 
                            Data.sourceToHandle[InputSource.RightHand], 
                            Data.sourceToHandle[InputSource.Head],
                            Data.sourceToHandle[InputSource.Gamepad]
                        });
                        if (!headsetHzUpdated && Data.sourceToIndex.ContainsKey(InputSource.Head)) {
                            int id = Data.sourceToIndex[InputSource.Head];
                            float hz = _vr.GetFloatTrackedDeviceProperty((uint) id, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
                            if(hz != 0)
                            {
                                headsetHzMs = (int) Math.Round(1000.0 / hz);
                                headsetHzUpdated = true;
                            }
                        }
                    }
                } else
                {
                    // Idle with attempted init
                    Thread.Sleep(2000);
                    _vr.Init();
                }
                if(_shouldShutDown)
                {
                    // Shutting down
                    _shouldShutDown = false;
                    initComplete = false;
                    _vr.AcknowledgeShutdown();
                    _vr.Shutdown();
                    Data.reset();
                    Debug.WriteLine("Shutting down!");
                    _openvrStatusAction.Invoke(false);
                }
            }
        }

        private void RegisterActions()
        {
            Action<InputDigitalActionData_t, InputActionInfo> SendDigitalInput = (data, info) =>
            {
                SendInput(data, info);
            };
            Action<InputAnalogActionData_t, InputActionInfo> StoreAnalogInput = (data, info) =>
            {
                Data.UpdateOrAddAnalogInputActionData(data, info);
            };
            Action<InputPoseActionData_t, InputActionInfo> StorePoseInput = (data, info) =>
            {
                Data.UpdateOrAddPoseInputActionData(data, info);
            };

            _vr.RegisterActionSet(GetAction());
            _vr.RegisterDigitalAction(GetAction("Proximity"), SendDigitalInput);
            
            _vr.RegisterDigitalAction(GetAction("TriggerClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("TriggerTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("TriggerValue"), StoreAnalogInput);
            _vr.RegisterDigitalAction(GetAction("AltTriggerClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("AltTriggerTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("AltTriggerValue"), StoreAnalogInput);
            _vr.RegisterDigitalAction(GetAction("ShoulderClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ShoulderTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("ShoulderValue"), StoreAnalogInput);
            _vr.RegisterDigitalAction(GetAction("AltShoulderClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("AltShoulderTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("AltShoulderValue"), StoreAnalogInput);

            _vr.RegisterDigitalAction(GetAction("ButtonAClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonATouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonBClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonBTouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonXClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonXTouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonYClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonYTouch"), SendDigitalInput);

            _vr.RegisterDigitalAction(GetAction("ButtonSystemClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonSystemTouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonApplicationMenuClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonApplicationMenuTouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonStartClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonStartTouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonBackClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonBackTouch"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonGuideClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonGuideTouch"), SendDigitalInput);

            _vr.RegisterDigitalAction(GetAction("TrackpadClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("TrackpadTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("TrackpadPosition"), StoreAnalogInput);
            _vr.RegisterAnalogAction(GetAction("TrackpadForce"), StoreAnalogInput);

            _vr.RegisterDigitalAction(GetAction("DirectionalPadUp"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("DirectionalPadLeft"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("DirectionalPadRight"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("DirectionalPadDown"), SendDigitalInput);

            _vr.RegisterDigitalAction(GetAction("JoystickClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("JoystickTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("JoystickPosition"), StoreAnalogInput);
            _vr.RegisterDigitalAction(GetAction("AltJoystickClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("AltJoystickTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("AltJoystickPosition"), StoreAnalogInput);

            _vr.RegisterDigitalAction(GetAction("GripClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("GripTouch"), SendDigitalInput);
            _vr.RegisterAnalogAction(GetAction("GripForce"), StoreAnalogInput);

            _vr.RegisterPoseAction(GetAction("Pose"), StorePoseInput);
            _vr.RegisterPoseAction(GetAction("Pose2"), StorePoseInput);
            _vr.RegisterPoseAction(GetAction("Pose3"), StorePoseInput);
        }

        private string GetAction(string action="")
        {
            var actionSet = "/actions/default";
            if (action.Length > 0) return $"{actionSet}/in/{action}";
            else return actionSet;
        }

        private void RegisterEvents()
        {
            _vr.RegisterEvent(EVREventType.VREvent_Quit, (data) => {
                _shouldShutDown = true;
                _stopRunning = true;
            });
            _vr.RegisterEvent(EVREventType.VREvent_TrackedDeviceActivated, (data) =>
            {
                Data.UpdateInputDeviceHandles();
                Data.UpdateDeviceIndices(data.trackedDeviceIndex);
                SendDeviceIds();
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_TrackedDeviceDeactivated, 
                EVREventType.VREvent_TrackedDeviceRoleChanged,
                EVREventType.VREvent_TrackedDeviceUpdated
            }, (data) => {
                Data.UpdateInputDeviceHandles();
                SendDeviceIds();
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_ChaperoneDataHasChanged, 
                EVREventType.VREvent_ChaperoneUniverseHasChanged 
            }, (data) => {
                SendPlayArea();
            });
            _vr.RegisterEvent(EVREventType.VREvent_PropertyChanged, (data) =>
            {
                // Look for things here that is useful, like battery states
                Debug.WriteLine(Enum.GetName(typeof(ETrackedDeviceProperty), data.data.property.prop)); 
                SendDeviceProperty(CommandEnum.DeviceProperty, (int) data.trackedDeviceIndex, data.data.property.prop);
            });
            _vr.RegisterEvent(EVREventType.VREvent_SteamVRSectionSettingChanged, (data) =>
            {
                SendResult("Debug", data.data);
            });
            _vr.RegisterEvents(new[] {
                EVREventType.VREvent_SceneApplicationChanged,
                EVREventType.VREvent_SceneApplicationStateChanged
            }, (data) => {
                SendApplicationInfo();
            });
            _vr.RegisterEvent(EVREventType.VREvent_EnterStandbyMode, (data) =>
            {
                // _server.SendMessageToAll("Entered standby.");
            });
            _vr.RegisterEvent(EVREventType.VREvent_LeaveStandbyMode, (data) =>
            {
                // _server.SendMessageToAll("Left standby.");
            });
        }
        #endregion

        #region Send Data
        private volatile string _currentAppId = "";
        private double _currentAppSessionTime = 0.0;

        private void SendDefaults(WebSocketSession session=null)
        {
            SendApplicationInfo(session);
            SendPlayArea(session);
            SendDeviceIds(session);
        }

        private void SendApplicationInfo(WebSocketSession session=null)
        {
            var appId = _vr.GetRunningApplicationId();
            if(appId != _currentAppId)
            {
                _currentAppId = appId;
                _currentAppSessionTime = Utils.NowUnixUTC();
            }
            var data = new Dictionary<string, dynamic>();
            data["id"] = appId;
            data["sessionStart"] = _currentAppSessionTime;
            SendResult(CommandEnum.ApplicationInfo, data, session);
        }

        private void SendPlayArea(WebSocketSession session=null)
        {
            var rect = _vr.GetPlayAreaRect();
            var size = _vr.GetPlayAreaSize();
            var height = _vr.GetFloatSetting(OpenVR.k_pch_CollisionBounds_Section, OpenVR.k_pch_CollisionBounds_WallHeight_Float);
            SendResult(CommandEnum.PlayArea, new PlayArea(rect, size, height));
        }

        private void SendDeviceIds(WebSocketSession session=null)
        {
            var data = new Dictionary<string, dynamic>();
            data["deviceToIndex"] = Data.deviceToIndex;
            data["sourceToIndex"] = Data.sourceToIndex;
            SendResult(CommandEnum.DeviceIds, data);
        }

        private void SendDeviceProperty(CommandEnum key, int deviceIndex, ETrackedDeviceProperty property, WebSocketSession session=null)
        {
            if (deviceIndex == -1) return; // Should not really happen, but means the device does not exist
            var index = (uint)deviceIndex;
            var propName = Enum.GetName(typeof(ETrackedDeviceProperty), property);
            if (propName == null) return; // This happens for vendor reserved properties (10000-10999)
            var data = new Dictionary<string, dynamic>();
            var propArray = propName.Split('_');
            var dataType = propArray.Last();
            var arrayType = dataType == "Array" ? propArray[propArray.Length - 2] : ""; // Matrix34, Int32, Float, Vector4, 
            object propertyValue = null;
            switch(dataType)
            {
                case "String": propertyValue = _vr.GetStringTrackedDeviceProperty(index, property); break;
                case "Bool": propertyValue = _vr.GetBooleanTrackedDeviceProperty(index, property); break;
                case "Float": propertyValue = _vr.GetFloatTrackedDeviceProperty(index, property); break;
                case "Matrix34": Debug.WriteLine($"{dataType} property: {propArray[1]}"); break;
                case "Uint64": propertyValue = _vr.GetLongTrackedDeviceProperty(index, property); break;
                case "Int32": propertyValue = _vr.GetIntegerTrackedDeviceProperty(index, property); break;
                case "Binary": Debug.WriteLine($"{dataType} property: {propArray[1]}"); break;
                case "Array": Debug.WriteLine($"{dataType}<{arrayType}> property: {propArray[1]}"); break;
                case "Vector3": Debug.WriteLine($"{dataType} property: {propArray[1]}"); break;
                default: Debug.WriteLine($"{dataType} unhandled property: {propArray[1]}"); break;
            }
            data["device"] = deviceIndex;
            data["name"] = propName;
            data["value"] = propertyValue;
            data["type"] = dataType;
            SendResult(key, data, session);
        }

        private void SendSetting(CommandEnum key, string section, string setting, WebSocketSession session= null) {
            // TODO: Add switch on type
            var value =_vr.GetFloatSetting(section, setting);
            var data = new Dictionary<string, dynamic>();
            data["section"] = section;
            data["setting"] = setting;
            data["value"] = value;
            SendResult(key, data, session);
        }
        #endregion

        public void Shutdown() {
            _openvrStatusAction = (status) => { };
            _server.ResetActions();
            _shouldShutDown = true;
            _server.Stop();
        }
    }
}