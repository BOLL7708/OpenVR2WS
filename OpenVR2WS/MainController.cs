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

namespace OpenVR2WS
{
    class MainController
    {
        private SuperServer _server = new SuperServer();
        private Properties.Settings _settings = Properties.Settings.Default;
        private EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;
        private StringEnumConverter _converter = new StringEnumConverter();

        // Data storage


        public MainController() {
            InitServer();

            _vr.SetDebugLogAction((message) => {
                Debug.WriteLine($"Debug log: {message}");
            });
            _vr.Init();

            InitWorkerThread();
        }

        #region WebSocketServer
        private void InitServer()
        {
            _server.MessageReceievedAction = (session, message) =>
            {
                // Debug.WriteLine($"Message received: {message}");
                var command = new Command();
                try { command = JsonConvert.DeserializeObject<Command>(message); }
                catch (Exception e) { Debug.WriteLine($"JSON Parsing Exception: {e.Message}"); }

                if (command.command != CommandEnum.None) HandleCommand(session, command);
                else _server.SendMessage(session, "Invalid command!");
                // TODO: If performing VR tasks, check it is actually initialized
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

        public void Test()
        {
            _server.SendMessageToAll("This is a test.");
        }

        // If session is null, it will send to all registered sessions
        private void SendResult(string key, Object data, WebSocketSession session = null, int device = -1) // TODO: This needs some class for actual delivery... to make nice JSON.
        {
            var result = new Dictionary<string, dynamic>();
            result["key"] = key;
            result["data"] = data;
            result["device"] = device;
            var jsonString = JsonConvert.SerializeObject(result, _converter);
            _server.SendMessage(session, jsonString);
        }

        private class Command
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandEnum command = CommandEnum.None;
            public string value = "";
            public int device = -1;
        }

        enum CommandEnum
        {
            None,
            CumulativeStats,
            PlayArea,
            ApplicationInfo,
            DeviceIds,
            DeviceProperty
        }

        private void HandleCommand(WebSocketSession session, Command command)
        {
            if (!_vr.IsInitialized()) return;
            switch(command.command)
            {
                case CommandEnum.None: break;
                case CommandEnum.CumulativeStats:
                    var stats = _vr.GetCumulativeStats();
                    SendResult("CumulativeStats", new CumulativeStats(stats), session);
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
                        SendDeviceProperty(command.device, property, session);
                    }
                    else SendResult("Error", $"Faulty property: {command.value}", session); // TODO: Convert to error func
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
        private void Worker()
        {
            Thread.CurrentThread.IsBackground = true;
            var initComplete = false;

            while(true)
            {
                if(_vr.IsInitialized())
                {
                    Thread.Sleep(50); // TODO: Connect to headset Hz
                    if (!initComplete)
                    {
                        // Happens once
                        initComplete = true;
                        _vr.LoadAppManifest("./app.vrmanifest");
                        _vr.LoadActionManifest("./actions.json");
                        Data.UpdateDeviceIndices();
                        Data.UpdateControllerRoles();
                        Data.UpdateInputDeviceHandles();
                        RegisterActions();
                        RegisterEvents();
                        SendDefaults();
                        Debug.WriteLine("Initialization complete!");
                    } else
                    {
                        // Happens every loop
                        _vr.UpdateEvents(false);
                        _vr.UpdateActionStates(new[] {
                            Data.inputDeviceHandles[EasyOpenVRSingleton.InputSource.LeftHand], 
                            Data.inputDeviceHandles[EasyOpenVRSingleton.InputSource.RightHand], 
                            Data.inputDeviceHandles[EasyOpenVRSingleton.InputSource.Head]
                        });
                    }
                } else
                {
                    // Idle with attempted init
                    Thread.Sleep(1000);
                    _vr.Init();
                }
                if(_shouldShutDown)
                {
                    // Shutting down
                    _shouldShutDown = false;
                    initComplete = false;
                    _vr.AcknowledgeShutdown();
                    _vr.Shutdown();
                    Debug.WriteLine("Shutting down!");
                }
            }
        }

        private void RegisterActions()
        {
            _vr.RegisterActionSet("/actions/default");
            _vr.RegisterDigitalAction($"/actions/default/in/TriggerClick", (data, handle) => { _server.SendMessageToAll($"Action for Trigger Click: {data.bState} ({data.bChanged}) {handle}"); });
            _vr.RegisterDigitalAction($"/actions/default/in/TriggerTouch", (data, handle) => { _server.SendMessageToAll($"Action for Trigger Touch: {data.bState} ({data.bChanged}) {handle}"); });
            // _vr.RegisterAnalogAction($"/actions/default/in/TriggerValue", (data, handle) => { _server.SendMessageToAll($"Action for Trigger Value: {data.x}, {data.y}, {data.z}"); });
        }

        private void RegisterEvents()
        {
            _vr.RegisterEvent(EVREventType.VREvent_Quit, (data) => {
                _shouldShutDown = true;
            });
            _vr.RegisterEvent(EVREventType.VREvent_TrackedDeviceActivated, (data) =>
            {
                Data.UpdateControllerRoles();
                Data.UpdateInputDeviceHandles();
                Data.UpdateDeviceIndices(data.trackedDeviceIndex);
                SendDeviceIds();
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_TrackedDeviceDeactivated, 
                EVREventType.VREvent_TrackedDeviceRoleChanged,
                EVREventType.VREvent_TrackedDeviceUpdated
            }, (data) => {
                Data.UpdateControllerRoles();
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
                SendDeviceProperty((int) data.trackedDeviceIndex, data.data.property.prop);
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
                // TODO: Start game timer to keep track of session length
            });


            _vr.RegisterEvent(EVREventType.VREvent_EnterStandbyMode, (data) =>
            {
                _server.SendMessageToAll("Entered standby.");
            });
            _vr.RegisterEvent(EVREventType.VREvent_LeaveStandbyMode, (data) =>
            {
                _server.SendMessageToAll("Left standby.");
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
                _currentAppSessionTime = Utils.NowMs();
            }
            var data = new Dictionary<string, dynamic>();
            data["id"] = appId;
            data["sessionStartMs"] = _currentAppSessionTime;
            SendResult("ApplicationInfo", data, session);
        }

        private void SendPlayArea(WebSocketSession session=null)
        {
            var rect = _vr.GetPlayAreaRect();
            var size = _vr.GetPlayAreaSize();
            SendResult("PlayArea", new PlayArea(rect, size));
        }

        private void SendDeviceIds(WebSocketSession session=null)
        {
            var data = new Dictionary<string, dynamic>();
            data["controllerRoles"] = Data.controllerRoles;
            data["deviceToIndex"] = Data.deviceToIndex;
            data["indexToDevice"] = Data.indexToDevice;
            data["inputDeviceHandles"] = Data.inputDeviceHandles;
            SendResult("DeviceIds", data);
        }

        private void SendDeviceProperty(int deviceIndex, ETrackedDeviceProperty property, WebSocketSession session=null)
        {
            if (deviceIndex == -1) return;
            var index = (uint)deviceIndex;
            var propName = Enum.GetName(typeof(ETrackedDeviceProperty), property);
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
            data["name"] = propName;
            data["value"] = propertyValue;
            data["type"] = dataType;
            SendResult("DeviceProperty", data, session, deviceIndex);
        }
        #endregion
    }
}