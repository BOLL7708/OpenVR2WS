using BOLL7708;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;

namespace OpenVR2WS
{
    class MainController
    {
        private SuperServer _server = new SuperServer();
        private Properties.Settings _settings = Properties.Settings.Default;
        private EasyOpenVRSingleton _vr = EasyOpenVRSingleton.Instance;

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
                _server.SendMessage(session, "Thanks!");
                // TODO: If performing VR tasks, check it is actually initialized
            };
            _server.StatusMessageAction = (status) =>
            {
                Debug.WriteLine($"Status received: {status}");
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

        private void SendResult(Object result) // TODO: This needs some class for actual delivery... to make nice JSON.
        {
             _server.SendObjectToAll(result);
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
                    Thread.Sleep(10); // TODO: Connect to headset Hz
                    if (!initComplete)
                    {
                        // Happens once
                        initComplete = true;
                        _vr.LoadAppManifest("./app.vrmanifest");
                        _vr.LoadActionManifest("./actions.json");
                        RegisterActions();
                        RegisterEvents();
                        Debug.WriteLine("Initialization complete!");
                    } else
                    {
                        // Happens every loop
                        _vr.UpdateEvents(false);
                        _vr.UpdateActionStates();
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
            _vr.RegisterActionSet("/actions/input");
            _vr.RegisterDigitalAction($"/actions/input/in/TriggerL", (data, handle) => { _server.SendMessageToAll($"Action for LEFT TRIGGER: {data.bState} ({data.bChanged})"); });
            _vr.RegisterDigitalAction($"/actions/input/in/TriggerR", (data, handle) => { _server.SendMessageToAll($"Action for RIGHT TRIGGER: {data.bState} ({data.bChanged})"); });
        }

        private void RegisterEvents()
        {
            _vr.RegisterEvent(EVREventType.VREvent_Quit, (data) => {
                _shouldShutDown = true;
            });
            _vr.RegisterEvent(EVREventType.VREvent_TrackedDeviceActivated, (data) =>
            {
                _server.SendMessageToAll("Update indexes, device properties? controller roles!");
            });
            _vr.RegisterEvents(new[] { 
                EVREventType.VREvent_TrackedDeviceDeactivated, 
                EVREventType.VREvent_TrackedDeviceRoleChanged 
            }, (data) => {
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
                EVREventType.VREvent_PropertyChanged 
            }, (data) => {
                _server.SendMessageToAll("Update device indexes and properties");
            });
            _vr.RegisterEvents(new[] {
                EVREventType.VREvent_SceneApplicationChanged,
                EVREventType.VREvent_SceneApplicationStateChanged
            }, (data) => {
                _server.SendMessageToAll("Update running application");
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
    }
}
