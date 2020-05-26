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

        private void SendResult(Object result)
        {
            // _server.SendMessageToAll(); // TODO: Figure out a general return format
        }
        #endregion

        #region VRWorkerThread
        private Thread _workerThread = null;
        private void InitWorkerThread()
        {
            _workerThread = new Thread(Worker);
            if (!_workerThread.IsAlive) _workerThread.Start();
        }

        private void Worker()
        {
            Thread.CurrentThread.IsBackground = true;
            var initComplete = false;

            while(true)
            {
                if(_vr.IsInitialized())
                {
                    Thread.Sleep(10);
                    if (!initComplete)
                    {
                        initComplete = true;
                        _vr.LoadAppManifest("./app.vrmanifest");
                        _vr.LoadActionManifest("./actions.json");
                        RegisterActions();
                        RegisterEvents();
                    } else
                    {
                        _vr.UpdateEvents(true);
                        _vr.UpdateActionStates();
                    }
                } else
                {
                    Thread.Sleep(1000);
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
            _vr.RegisterEvent(EVREventType.VREvent_SceneApplicationChanged, (data) => {
                var appId = _vr.GetRunningApplicationId();
                _server.SendMessageToAll($"Application ID is: {appId}"); // Replace this with function call SendResult()
            });
            _vr.RegisterEvent(EVREventType.VREvent_EnterStandbyMode, (data) =>
            {
                _server.SendMessageToAll("Entered standby.");
            });
        }
        #endregion
    }
}
