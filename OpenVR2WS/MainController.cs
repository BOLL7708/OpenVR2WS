using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EasyFramework;
using EasyOpenVR;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpenVR2WS.Output;
using OpenVR2WS.Properties;
using SuperSocket.WebSocket.Server;
using Valve.VR;
using static EasyOpenVR.EasyOpenVRSingleton;

namespace OpenVR2WS
{
    internal class MainController
    {
        private readonly SuperServer _server = new();
        private readonly Settings _settings = Settings.Default;
        private readonly EasyOpenVRSingleton _vr = Instance;
        private readonly StringEnumConverter _converter = new();
        private Action<bool> _openvrStatusAction;

        // Data storage


        public MainController(Action<SuperServer.ServerStatus, int> serverStatus, Action<bool> openvrStatus)
        {
            _openvrStatusAction += openvrStatus;
            Data.Reset();
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
            _server.StatusAction += serverStatus;
            _server.MessageReceivedAction += (session, message) =>
            {
                var command = new Command();
                try { command = JsonConvert.DeserializeObject<Command>(message); }
                catch (Exception e) { Debug.WriteLine($"JSON Parsing Exception: {e.Message}"); }

                if (command.key != CommandEnum.None) HandleCommand(session, command);
                else SendResult("InvalidCommand", new GenericResponse() { message=message, success=false }, session);
            };
            _server.StatusMessageAction += (session, connected, status) =>
            {
                Debug.WriteLine($"Status received: {status}");
                if(connected && _vr.IsInitialized())
                {
                    SendDefaults(session);
                }
            };
            RestartServer(_settings.Port);
        }

        public async void RestartServer(int port)
        {
            await _server.Start(port);
        }

        public void ReregisterActions()
        {
            RegisterActions();
        }

        // If session is null, it will send to all registered sessions
        private void SendResult(CommandEnum command, Object data=null, WebSocketSession session = null)
        {
            var key = Enum.GetName(typeof(CommandEnum), command);
            SendResult(key, data, session);
        }
        private void SendResult(string key, Object data=null, WebSocketSession session = null)
        {
            var result = new Dictionary<string, dynamic>
            {
                ["key"] = key,
                ["data"] = data
            };
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
            public string value3 = "";
            public string value4 = "";
            public string value5 = "";
            public string value6 = "";
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
            Setting,
            RemoteSetting,
            FindOverlay,
            Relay,
            MoveSpace
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
                    Data.UpdateInputDeviceHandles();
                    Data.UpdateDeviceIndices();
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
                case CommandEnum.RemoteSetting:
                    var remoteSettingResponse = ApplyRemoteSetting(command);
                    SendResult(command.key, remoteSettingResponse, session);
                    break;
                case CommandEnum.FindOverlay:
                    var overlayHandle = _vr.FindOverlay(command.value);
                    var overlayResult = new Dictionary<string, dynamic>
                    {
                        { "handle", overlayHandle },
                        { "key", command.value }
                    };
                    SendResult(command.key, overlayResult, session);
                    break;
                case CommandEnum.Relay:
                    var relayRelay = new Dictionary<string, dynamic>
                    {
                        { "password", command.value },
                        { "user", command.value2 },
                        { "key", command.value3 },
                        { "data", command.value4 }
                    };
                    SendResult(command.key, relayRelay);
                    break;
                case CommandEnum.MoveSpace:
                    var moveSpaceResponse = ApplyMoveSpace(command);
                    SendResult(command.key, moveSpaceResponse, session);
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
                        _vr.AddApplicationManifest("./app.vrmanifest", "boll7708.openvr2ws", true);
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
                            Data.sourceToHandle[InputSource.Head],
                            Data.sourceToHandle[InputSource.Chest],
                            Data.sourceToHandle[InputSource.LeftShoulder],
                            Data.sourceToHandle[InputSource.RightShoulder],
                            Data.sourceToHandle[InputSource.LeftElbow],
                            Data.sourceToHandle[InputSource.RightElbow],
                            Data.sourceToHandle[InputSource.LeftHand], 
                            Data.sourceToHandle[InputSource.RightHand],
                            Data.sourceToHandle[InputSource.Waist],
                            Data.sourceToHandle[InputSource.LeftKnee],
                            Data.sourceToHandle[InputSource.RightKnee],
                            Data.sourceToHandle[InputSource.LeftFoot],
                            Data.sourceToHandle[InputSource.RightFoot],
                            Data.sourceToHandle[InputSource.Camera],
                            Data.sourceToHandle[InputSource.Gamepad]
                        });
                        if (_settings.UseDevicePoses) {
                            var poses = _vr.GetDeviceToAbsoluteTrackingPose();
                            for(var i=0; i<poses.Length; i++)
                            {
                                Data.UpdateOrAddPoseData(poses[i], i);
                            }
                        }
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
                    Data.Reset();
                    Debug.WriteLine("Shutting down!");
                    _openvrStatusAction.Invoke(false);
                }
            }
        }

        private void RegisterActions()
        {
            void SendDigitalInput(InputDigitalActionData_t data, InputActionInfo info)
            {
                SendInput(data, info);
            }
            void StoreAnalogInput(InputAnalogActionData_t data, InputActionInfo info)
            {
                Data.UpdateOrAddAnalogInputActionData(data, info);
            }
            void StorePoseInput(InputPoseActionData_t data, InputActionInfo info)
            {
                Data.UpdateOrAddPoseInputActionData(data, info);
            }

            _vr.ClearInputActions();
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

            _vr.RegisterDigitalAction(GetAction("ButtonPowerClick"), SendDigitalInput);
            _vr.RegisterDigitalAction(GetAction("ButtonPowerTouch"), SendDigitalInput);
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
            
            if(!_settings.UseDevicePoses)
            {
                _vr.RegisterPoseAction(GetAction("Pose"), StorePoseInput);
                _vr.RegisterPoseAction(GetAction("Pose2"), StorePoseInput);
                _vr.RegisterPoseAction(GetAction("Pose3"), StorePoseInput);
            }
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
                // Debug.WriteLine(Enum.GetName(typeof(ETrackedDeviceProperty), data.data.property.prop)); 
                SendDeviceProperty(CommandEnum.DeviceProperty, (int) data.trackedDeviceIndex, data.data.property.prop);
            });
            _vr.RegisterEvent(EVREventType.VREvent_SteamVRSectionSettingChanged, (data) =>
            {
                // SendResult("Debug", data);
                var fakeData = new Dictionary<string, dynamic>();
                fakeData.Add("Issue", "https://github.com/ValveSoftware/openvr/issues/1335");
                SendResult(CommandEnum.Setting, fakeData);
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
            _vr.RegisterEvents(new[] {
                EVREventType.VREvent_Compositor_ChaperoneBoundsShown,
                EVREventType.VREvent_Compositor_ChaperoneBoundsHidden,
                EVREventType.VREvent_RoomViewShown,
                EVREventType.VREvent_RoomViewHidden,
                EVREventType.VREvent_TrackedCamera_StartVideoStream,
                EVREventType.VREvent_TrackedCamera_PauseVideoStream,
                EVREventType.VREvent_TrackedCamera_ResumeVideoStream,
                EVREventType.VREvent_TrackedCamera_StopVideoStream
            }, (data) => {
                SendEvent((EVREventType) data.eventType);
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
            var data = new Dictionary<string, dynamic>
            {
                ["id"] = appId,
                ["sessionStart"] = _currentAppSessionTime
            };
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
            var data = new Dictionary<string, dynamic>
            {
                ["deviceToIndex"] = Data.deviceToIndex,
                ["sourceToIndex"] = Data.sourceToIndex
            };
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

        private void SendSetting(CommandEnum key, string section, string setting, WebSocketSession session = null) {
            // TODO: Add switch on type
            var value =_vr.GetFloatSetting(section, setting);
            var data = new Dictionary<string, dynamic>
            {
                ["section"] = section,
                ["setting"] = setting,
                ["value"] = value
            };
            SendResult(key, data, session);
        }

        private void SendEvent(EVREventType eventType, WebSocketSession session = null)
        {
            var data = new Dictionary<string, dynamic>();
            try
            {
                data["type"] = Enum.GetName(typeof(EVREventType), eventType).Replace("VREvent_", "");
                SendResult("event", data, session);
            }
            catch (Exception e) {
                Debug.WriteLine($"Could not get name for enum coming in from SteamVR: {eventType}, {e.Message}");
            }
        }
        #endregion

        private GenericResponse ApplyRemoteSetting(Command command) {
            var response = new GenericResponse();
            var canSet = CheckRemoteSetting(command, ref response);
            if (!canSet) return response;

            var settingSuccess = ApplySetting(command.value2, command.value3, command.value4, command.value5);

            if (!settingSuccess)
            {
                response.message = $"Failed to set {command.value2}/{command.value3} to {command.value4}.";
                return response;
            }

            response.message = $"Succeeded setting {command.value2}/{command.value3} to {command.value4}.";
            response.success = true;
            return response;
        }

        private GenericResponse ApplyMoveSpace(Command command) {
            var response = new GenericResponse();
            var canSet = CheckRemoteSetting(command, ref response);
            if (!canSet) return response;

            var newPos = new HmdVector3_t();
            float x, y, z;
            bool moveChaperone;
            try
            {
                x = float.Parse(command.value2);
                y = float.Parse(command.value3);
                z = float.Parse(command.value4);
                moveChaperone = bool.Parse(command.value5);
            } catch (Exception e) {
                response.message = $"Could not parse values for move (floats: value2, value3, value4, boolean: value5): {e.Message}";
                return response;
            }
            newPos.v0 = x;
            newPos.v1 = y;
            newPos.v2 = z;
            var success = _vr.MoveUniverse(newPos, moveChaperone, true);
            if (success) {
                response.success = true;
                response.message = "Moved space successfully.";
            } else
            {
                response.message = "Failed to move space.";
            }
            return response;
        }

        private bool CheckRemoteSetting(Command command, ref GenericResponse response) {
            if (!_settings.RemoteSettings)
            {
                response.message = $"The command '{command.key}' relies on Remote Settings which is disabled. Enable it in the application interface.";
                return false;
            }
            if (!command.value.Equals(_settings.RemoteSettingsPasswordHash))
            {
                response.message = "Password string did not match, b64-encode a binary SHA256 hash.";
                return false;
            }
            return true;
        }

        private bool ApplySetting(string section, string setting, string value, string type) {
            var boolSuccess = bool.TryParse(value, out var boolValue);
            var intSuccess = int.TryParse(value, out var intValue);
            var floatSuccess = float.TryParse(value, out var floatValue);

            if (type.Length > 0) {
                switch (type)
                {
                    case "String":
                        return _vr.SetStringSetting(section, setting, value);
                    case "Bool":
                        return _vr.SetBoolSetting(section, setting, boolValue);
                    case "Float":
                        return _vr.SetFloatSetting(section, setting, floatValue);
                    case "Int32":
                        return _vr.SetIntSetting(section, setting, intValue);
                    default:
                        return false;
                }
            } else {
                if (boolSuccess) return _vr.SetBoolSetting(section, setting, boolValue);
                else if (intSuccess) return _vr.SetIntSetting(section, setting, intValue);
                else if (floatSuccess) return _vr.SetFloatSetting(section, setting, floatValue);
                else return _vr.SetStringSetting(section, setting, value);
            }
        }

        public async void Shutdown() {
            _openvrStatusAction += (status) => { };
            await _server.Stop();
            _shouldShutDown = true;
        }
    }
}