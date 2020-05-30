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
                Debug.WriteLine($"Message received: {message}");
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
                    SendApplicationId(session);
                    SendPlayArea(session);
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
        private void SendResult(string key, Object data, WebSocketSession session = null, uint[] devices = null) // TODO: This needs some class for actual delivery... to make nice JSON.
        {
            var result = new Dictionary<string, dynamic>();
            if (devices == null) devices = new uint[0];
            result["key"] = key;
            result["data"] = data;
            result["devices"] = devices;
            _server.SendObject(session, result);
        }

        private class Command
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandEnum command = CommandEnum.None;
            public uint[] devices = new uint[0];
        }

        enum CommandEnum
        {
            None,
            CumulativeStats,
            PlayArea,
            ApplicationId
        }

        private void HandleCommand(WebSocketSession session, Command command)
        {
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
                case CommandEnum.ApplicationId:
                    SendApplicationId(session);
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
                        SendApplicationId();
                        SendPlayArea();
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
                _server.SendMessageToAll("Update indexes, device properties? controller roles!");
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_TrackedDeviceDeactivated, 
                EVREventType.VREvent_TrackedDeviceRoleChanged 
            }, (data) => {
                Data.UpdateControllerRoles();
                _server.SendMessageToAll("Update controller roles");
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_ChaperoneDataHasChanged, 
                EVREventType.VREvent_ChaperoneUniverseHasChanged 
            }, (data) => {
                _server.SendMessageToAll("Update chaperone");
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_TrackedDeviceUpdated,
                EVREventType.VREvent_PropertyChanged // TODO: Spammy...
            }, (data) => {
                _server.SendMessageToAll($"Update device indexes and properties, index: {data.trackedDeviceIndex}");
            });
            _vr.RegisterEvents(new[] {
                EVREventType.VREvent_SceneApplicationChanged,
                EVREventType.VREvent_SceneApplicationStateChanged
            }, (data) => {
                SendApplicationId();
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

        #region Private Helper Functions
        private volatile string _currentAppId = "";
        private double _currentAppSessionTime = 0.0;
        private void SendApplicationId(WebSocketSession session=null)
        {
            var appId = _vr.GetRunningApplicationId();
            if(appId != _currentAppId)
            {
                _currentAppId = appId;
                _currentAppSessionTime = Utils.NowMs();
            }
            SendResult("ApplicationId", appId, session);
        }

        private void SendPlayArea(WebSocketSession session=null)
        {
            var rect = _vr.GetPlayAreaRect();
            var size = _vr.GetPlayAreaSize();
            SendResult("PlayArea", new PlayArea(rect, size));
        }
        #endregion
    }
}