using SuperSocket.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVR2WS
{
    class SuperServer
    {
        private WebSocketServer _server = new WebSocketServer();
        private HashSet<WebSocketSession> _sessions = new HashSet<WebSocketSession>(); // Was getting crashes when loading all sessions from _server directly
        private readonly object _sessionsLock = new object();

        #region Actions
        public Action<WebSocketSession, string> MessageReceievedAction { get; set; } = (session, message) =>
        {
            Debug.WriteLine($"SuperServer.MessageReceivedAction not set, missed message: {message}");
        };
        public Action<WebSocketSession, byte[]> DataReceievedAction { get; set; } = (session, data) =>
        {
            Debug.WriteLine($"SuperServer.DataReceivedAction not set, missed data: {data.Length}");
        };
        public Action<string> StatusMessageAction { get; set; } = (message) =>
        {
            Debug.WriteLine($"SuperServer.StatusMessageAction not set, missed status: {message}");
        };
        #endregion

        public SuperServer(int port = 0)
        {
            if (port != 0) Start(port);
        }

        #region Manage
        public void Start(int port)
        {
            // Stop in case of already running
            Stop();

            // Start
            _server.Setup(port);
            _server.NewSessionConnected += Server_NewSessionConnected;
            _server.NewMessageReceived += Server_NewMessageReceived;
            _server.NewDataReceived += Server_NewDataReceived;
            _server.SessionClosed += Server_SessionClosed;
            _server.Start();
        }
        public void Stop()
        {
            _server.Dispose();
            _server.Stop();
        }
        #endregion

        #region Listeners 
        private void Server_NewSessionConnected(WebSocketSession session)
        {
            lock(_sessionsLock)
            {
                _sessions.Add(session);
            }
            StatusMessageAction.Invoke($"New session connected: {session.SessionID}");
        }

        private void Server_NewMessageReceived(WebSocketSession session, string value)
        {
            MessageReceievedAction.Invoke(session, value);
        }

        private void Server_NewDataReceived(WebSocketSession session, byte[] value)
        {
            DataReceievedAction.Invoke(session, value);
        }

        private void Server_SessionClosed(WebSocketSession session, SuperSocket.SocketBase.CloseReason value)
        {
            lock (_sessionsLock)
            {
                _sessions.Remove(session);
            }
            StatusMessageAction.Invoke($"Session closed: {session.SessionID}");
        }
        #endregion

        #region Send
        public void SendMessage(WebSocketSession session, string message)
        {
            if (_server.State != SuperSocket.SocketBase.ServerState.Running) return;
            if (session != null && session.Connected) session.Send(message);
        }
        public void SendMessageToAll(string message)
        {
            lock (_sessionsLock)
            {
                foreach (var session in _sessions)
                {
                    SendMessage(session, message);
                }
            }
        }
        public void SendObject(WebSocketSession session, object obj)
        {
            var json = _server.JsonSerialize(obj);
            SendMessage(session, json);
        }
        public void SendObjectToAll(object obj)
        {
            var json = _server.JsonSerialize(obj);
            SendMessageToAll(json);
        }
        #endregion
    }
}
