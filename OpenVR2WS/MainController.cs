using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyFramework;
using EasyOpenVR;
using OpenVR2WS.Input;
using OpenVR2WS.Output;
using OpenVR2WS.Properties;
using SuperSocket.WebSocket.Server;
using Valve.VR;
using static EasyOpenVR.EasyOpenVRSingleton;
using TypeEnum = OpenVR2WS.Input.TypeEnum;

namespace OpenVR2WS;

[SupportedOSPlatform("windows7.0")]
internal class MainController
{
    private readonly SuperServer _server = new();
    private readonly Settings _settings = Settings.Default;
    private readonly EasyOpenVRSingleton _vr = Instance;
    private Action<bool> _openvrStatusAction;

    public MainController(Action<SuperServer.ServerStatus, int> serverStatus, Action<bool> openvrStatus)
    {
        _openvrStatusAction += openvrStatus;
        DataStore.Reset();

        _vr.SetDebugLogAction((message) => { Debug.WriteLine($"Debug log: {message}"); });
        _vr.Init();

        InitServer(serverStatus);
        InitWorkerThread();
    }

    #region WebSocketServer

    private void InitServer(Action<SuperServer.ServerStatus, int> serverStatus)
    {
        Task.Delay(500).Wait();
        _server.StatusAction += serverStatus;
        _server.MessageReceivedAction += (session, message) =>
        {
            var command = new Request();
            try
            {
                command = JsonSerializer.Deserialize<Request>(message, JsonOptions.get());
            }
            catch (Exception e)
            {
                Debug.WriteLine($"JSON Parsing Exception: {e.Message}");
            }

            if (command != null && command.Key != RequestKeyEnum.None) HandleCommand(session, command);
            else SendResult(Response.CreateError($"InvalidCommand: {message}"), session);
        };
        _server.StatusMessageAction += (session, connected, status) =>
        {
            Debug.WriteLine($"Status received: {status}");
            if (connected && _vr.IsInitialized())
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
    private void SendCommandResult(RequestKeyEnum requestKey, dynamic? data = null, WebSocketSession? session = null)
    {
        SendResult(Response.CreateCommand(requestKey, data), session);
    }

    private void SendResult(Response response, WebSocketSession? session = null)
    {
        var jsonString = "";
        try
        {
            jsonString = JsonSerializer.Serialize(response, JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not serialize output for {response.Type}|{response.Key}: {e.Message}");
        }

        if (jsonString != "") _server.SendMessage(session, jsonString);
    }

    private void SendInput(ResponseTypeEnum type, InputDigitalActionData_t data, InputActionInfo info, WebSocketSession? session = null)
    {
        var source = DataStore.handleToSource[info.sourceHandle];
        var output = new JsonInputDigital(source, data, info);
        SendResult(Response.Create(type, output), session);
    }

    private void HandleCommand(WebSocketSession? session, Request command)
    {
        // Debug.WriteLine($"Command receieved: {Enum.GetName(typeof(CommandEnum), command.key)}");
        if (_stopRunning || !_vr.IsInitialized()) return;
        switch (command.Key)
        {
            case RequestKeyEnum.None: break;
            case RequestKeyEnum.CumulativeStats:
            {
                var stats = _vr.GetCumulativeStats();
                SendCommandResult(command.Key, new JsonCumulativeStats(stats), session);
                break;
            }
            case RequestKeyEnum.PlayArea:
                SendPlayArea(session);
                break;
            case RequestKeyEnum.ApplicationInfo:
                SendApplicationInfo(session);
                break;
            case RequestKeyEnum.DeviceIds:
                DataStore.UpdateInputDeviceHandles();
                DataStore.UpdateDeviceIndices();
                SendDeviceIds(session);
                break;
            case RequestKeyEnum.DeviceProperty:
            {
                DataDeviceProperty? data = null;
                try
                {
                    data = JsonSerializer.Deserialize<DataDeviceProperty>(command.Data?.GetRawText() ?? "", JsonOptions.get());
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
                }

                if (data != null && data.Property != ETrackedDeviceProperty.Prop_Invalid)
                {
                    SendDeviceProperty(command.Key, data.DeviceId, data.Property, session);
                }
                else SendResult(Response.CreateError($"Faulty property: {data?.Property}"), session);

                break;
            }
            case RequestKeyEnum.InputAnalog:
                SendCommandResult(command.Key, DataStore.analogInputActionData, session);
                break;
            case RequestKeyEnum.InputPose:
                SendCommandResult(command.Key, DataStore.poseInputActionData, session);
                break;
            case RequestKeyEnum.Setting:
            {
                DataSetting? data = null;
                try
                {
                    data = JsonSerializer.Deserialize<DataSetting>(command.Data?.GetRawText() ?? "", JsonOptions.get());
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
                }

                if (data != null) SendSetting(command.Key, data.Section, data.Setting, data.Type, session);
                else SendResult(Response.CreateError("Input was invalid."), session); // TODO: Include response with shape of data?
                break;
            }
            case RequestKeyEnum.RemoteSetting:
            {
                DataRemoteSetting? data = null;
                try
                {
                    data = JsonSerializer.Deserialize<DataRemoteSetting>(command.Data?.GetRawText() ?? "", JsonOptions.get());
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
                }

                if (data != null)
                {
                    var remoteSettingResponse = ApplyRemoteSetting(data);
                    remoteSettingResponse.Key = command.Key;
                    SendResult(remoteSettingResponse, session);
                }
                else SendResult(Response.CreateError("Input was invalid."), session); // TODO: Include response with shape of data?

                break;
            }
            case RequestKeyEnum.FindOverlay:
            {
                SendFoundOverlay(command);
                break;
            }
            case RequestKeyEnum.MoveSpace:
            {
                DataMoveSpace? data = null;
                try
                {
                    data = JsonSerializer.Deserialize<DataMoveSpace>(command.Data?.GetRawText() ?? "", JsonOptions.get());
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
                }

                if (data != null)
                {
                    var moveSpaceResponse = ApplyMoveSpace(data);
                    SendCommandResult(command.Key, moveSpaceResponse, session);
                }
                else SendResult(Response.CreateError("Input was invalid."), session); // TODO: Include response with shape of data?

                break;
            }
        }
    }

    #endregion

    #region VRWorkerThread

    private Thread? _workerThread;

    private void InitWorkerThread()
    {
        Task.Delay(1000).Wait();
        _workerThread = new Thread(Worker);
        if (!_workerThread.IsAlive) _workerThread.Start();
    }

    private volatile bool _shouldShutDown;
    private volatile bool _stopRunning;

    private void Worker()
    {
        Thread.CurrentThread.IsBackground = true;
        var initComplete = false;
        var headsetHzUpdated = false;
        var headsetHzMs = 1000 / 90;

        while (true)
        {
            if (_vr.IsInitialized())
            {
                Thread.Sleep(headsetHzMs);
                if (!initComplete)
                {
                    // Happens once
                    initComplete = true;
                    _stopRunning = false;
                    _vr.AddApplicationManifest("./app.vrmanifest", "boll7708.openvr2ws", true);
                    _vr.LoadActionManifest("./actions.json");
                    DataStore.UpdateDeviceIndices();
                    DataStore.UpdateInputDeviceHandles();
                    RegisterActions();
                    RegisterEvents();
                    SendDefaults();
                    Debug.WriteLine("Initialization complete!");
                    _openvrStatusAction.Invoke(true);
                }
                else
                {
                    // Happens every loop
                    _vr.UpdateEvents();
                    _vr.UpdateActionStates([
                        DataStore.sourceToHandle[InputSource.Head],
                        DataStore.sourceToHandle[InputSource.Chest],
                        DataStore.sourceToHandle[InputSource.LeftShoulder],
                        DataStore.sourceToHandle[InputSource.RightShoulder],
                        DataStore.sourceToHandle[InputSource.LeftElbow],
                        DataStore.sourceToHandle[InputSource.RightElbow],
                        DataStore.sourceToHandle[InputSource.LeftHand],
                        DataStore.sourceToHandle[InputSource.RightHand],
                        DataStore.sourceToHandle[InputSource.Waist],
                        DataStore.sourceToHandle[InputSource.LeftKnee],
                        DataStore.sourceToHandle[InputSource.RightKnee],
                        DataStore.sourceToHandle[InputSource.LeftFoot],
                        DataStore.sourceToHandle[InputSource.RightFoot],
                        DataStore.sourceToHandle[InputSource.Camera],
                        DataStore.sourceToHandle[InputSource.Gamepad]
                    ]);
                    if (_settings.UseDevicePoses)
                    {
                        var poses = _vr.GetDeviceToAbsoluteTrackingPose();
                        for (var i = 0; i < poses.Length; i++)
                        {
                            DataStore.UpdateOrAddPoseData(poses[i], i);
                        }
                    }

                    if (!headsetHzUpdated && DataStore.sourceToIndex.TryGetValue(InputSource.Head, out var id))
                    {
                        var hz = _vr.GetFloatTrackedDeviceProperty((uint)id,
                            ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
                        if (hz != 0)
                        {
                            headsetHzMs = (int)Math.Round(1000.0 / hz);
                            headsetHzUpdated = true;
                        }
                    }
                }
            }
            else
            {
                // Idle with attempted init
                Thread.Sleep(2000);
                _vr.Init();
            }

            if (!_shouldShutDown) continue;
            // Shutting down
            _shouldShutDown = false;
            initComplete = false;
            _vr.AcknowledgeShutdown();
            _vr.Shutdown();
            DataStore.Reset();
            Debug.WriteLine("Shutting down!");
            _openvrStatusAction.Invoke(false);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private void RegisterActions()
    {
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

        if (_settings.UseDevicePoses) return;
        _vr.RegisterPoseAction(GetAction("Pose"), StorePoseInput);
        _vr.RegisterPoseAction(GetAction("Pose2"), StorePoseInput);
        _vr.RegisterPoseAction(GetAction("Pose3"), StorePoseInput);

        return;

        void StorePoseInput(InputPoseActionData_t data, InputActionInfo info)
        {
            DataStore.UpdateOrAddPoseInputActionData(data, info);
        }

        void StoreAnalogInput(InputAnalogActionData_t data, InputActionInfo info)
        {
            DataStore.UpdateOrAddAnalogInputActionData(data, info);
        }

        void SendDigitalInput(InputDigitalActionData_t data, InputActionInfo info)
        {
            SendInput(ResponseTypeEnum.InputDigital, data, info);
        }
    }

    private static string GetAction(string action = "")
    {
        const string actionSet = "/actions/default";
        return action.Length > 0 ? $"{actionSet}/in/{action}" : actionSet;
    }

    private void RegisterEvents()
    {
        _vr.RegisterEvent(EVREventType.VREvent_Quit, (_) =>
        {
            _shouldShutDown = true;
            _stopRunning = true;
        });
        _vr.RegisterEvent(EVREventType.VREvent_TrackedDeviceActivated, (data) =>
        {
            DataStore.UpdateInputDeviceHandles();
            DataStore.UpdateDeviceIndices(data.trackedDeviceIndex);
            SendDeviceIds();
        });
        _vr.RegisterEvents([
            EVREventType.VREvent_TrackedDeviceDeactivated,
            EVREventType.VREvent_TrackedDeviceRoleChanged,
            EVREventType.VREvent_TrackedDeviceUpdated
        ], (_) =>
        {
            DataStore.UpdateInputDeviceHandles();
            DataStore.UpdateDeviceIndices();
            SendDeviceIds();
        });
        _vr.RegisterEvents([
            EVREventType.VREvent_ChaperoneDataHasChanged,
            EVREventType.VREvent_ChaperoneUniverseHasChanged
        ], (_) => { SendPlayArea(); });
        _vr.RegisterEvent(EVREventType.VREvent_PropertyChanged, (data) =>
        {
            // Look for things here that is useful, like battery states
            // Debug.WriteLine(Enum.GetName(typeof(ETrackedDeviceProperty), data.data.property.prop)); 
            SendDeviceProperty(RequestKeyEnum.DeviceProperty, (int)data.trackedDeviceIndex, data.data.property.prop);
        });
        _vr.RegisterEvent(EVREventType.VREvent_SteamVRSectionSettingChanged, (_) =>
        {
            // SendResult("Debug", data);
            var fakeData = new Dictionary<string, dynamic> { { "Issue", "https://github.com/ValveSoftware/openvr/issues/1335" } };
            SendCommandResult(RequestKeyEnum.Setting, fakeData);
        });
        _vr.RegisterEvents([
            EVREventType.VREvent_SceneApplicationChanged,
            EVREventType.VREvent_SceneApplicationStateChanged
        ], (_) => { SendApplicationInfo(); });
        _vr.RegisterEvent(EVREventType.VREvent_EnterStandbyMode, (_) =>
        {
            // _server.SendMessageToAll("Entered standby.");
        });
        _vr.RegisterEvent(EVREventType.VREvent_LeaveStandbyMode, (_) =>
        {
            // _server.SendMessageToAll("Left standby.");
        });
        _vr.RegisterEvents([
            EVREventType.VREvent_Compositor_ChaperoneBoundsShown,
            EVREventType.VREvent_Compositor_ChaperoneBoundsHidden,
            EVREventType.VREvent_RoomViewShown,
            EVREventType.VREvent_RoomViewHidden,
            EVREventType.VREvent_TrackedCamera_StartVideoStream,
            EVREventType.VREvent_TrackedCamera_PauseVideoStream,
            EVREventType.VREvent_TrackedCamera_ResumeVideoStream,
            EVREventType.VREvent_TrackedCamera_StopVideoStream
        ], (data) => { SendEvent((EVREventType)data.eventType); });
    }

    #endregion

    #region Send Data

    private volatile string _currentAppId = "";
    private double _currentAppSessionTime;

    private void SendDefaults(WebSocketSession? session = null)
    {
        SendApplicationInfo(session);
        SendPlayArea(session);
        DataStore.UpdateInputDeviceHandles();
        DataStore.UpdateDeviceIndices();
        SendDeviceIds(session);
    }

    private void SendApplicationInfo(WebSocketSession? session = null)
    {
        var appId = _vr.GetRunningApplicationId();
        if (appId != _currentAppId)
        {
            _currentAppId = appId;
            _currentAppSessionTime = Utils.NowUnixUTC();
        }

        var data = new JsonApplicationInfo(_currentAppId, _currentAppSessionTime);
        SendCommandResult(RequestKeyEnum.ApplicationInfo, data, session);
    }

    private void SendPlayArea(WebSocketSession? session = null)
    {
        var rect = _vr.GetPlayAreaRect();
        var size = _vr.GetPlayAreaSize();
        var height = _vr.GetFloatSetting(OpenVR.k_pch_CollisionBounds_Section,
            OpenVR.k_pch_CollisionBounds_WallHeight_Float);
        SendCommandResult(RequestKeyEnum.PlayArea, new JsonPlayArea(rect, size, height), session);
    }

    private void SendDeviceIds(WebSocketSession? session = null)
    {
        var data = new JsonDeviceIds(DataStore.deviceToIndex.ToDictionary(), DataStore.sourceToIndex.ToDictionary());
        SendCommandResult(RequestKeyEnum.DeviceIds, data, session);
    }

    private void SendDeviceProperty(RequestKeyEnum requestKey, int deviceIndex, ETrackedDeviceProperty property,
        WebSocketSession? session = null)
    {
        if (deviceIndex == -1) return; // Should not really happen, but means the device does not exist
        var index = (uint)deviceIndex;
        var propName = Enum.GetName(typeof(ETrackedDeviceProperty), property);
        if (propName == null) return; // This happens for vendor reserved properties (10000-10999)
        var propArray = propName.Split('_');
        var dataType = propArray.Last();
        var arrayType = dataType == "Array" ? propArray[^2] : ""; // Matrix34, Int32, Float, Vector4, 
        Enum.TryParse(dataType, out Output.TypeEnum dataTypeEnum);
        dynamic? propertyValue = null;
        switch (dataTypeEnum)
        {
            case Output.TypeEnum.String:
                propertyValue = _vr.GetStringTrackedDeviceProperty(index, property);
                break;
            case Output.TypeEnum.Bool:
                propertyValue = _vr.GetBooleanTrackedDeviceProperty(index, property);
                break;
            case Output.TypeEnum.Float:
                propertyValue = _vr.GetFloatTrackedDeviceProperty(index, property);
                break;
            case Output.TypeEnum.Matrix34:
                Debug.WriteLine($"{dataType} property: {propArray[1]}");
                break;
            case Output.TypeEnum.Uint64:
                propertyValue = _vr.GetLongTrackedDeviceProperty(index, property);
                break;
            case Output.TypeEnum.Int32:
                propertyValue = _vr.GetIntegerTrackedDeviceProperty(index, property);
                break;
            case Output.TypeEnum.Binary:
                Debug.WriteLine($"{dataType} property: {propArray[1]}");
                break;
            case Output.TypeEnum.Array:
                Debug.WriteLine($"{dataType}<{arrayType}> property: {propArray[1]}");
                break;
            case Output.TypeEnum.Vector3:
                Debug.WriteLine($"{dataType} property: {propArray[1]}");
                break;
            default:
                Debug.WriteLine($"{dataType} unhandled property: {propArray[1]}");
                break;
        }

        var data = new JsonDeviceProperty(deviceIndex, propName, propertyValue, dataType);
        SendCommandResult(requestKey, data, session);
    }

    private void SendSetting(RequestKeyEnum requestKey, string section, string setting, Output.TypeEnum type = Output.TypeEnum.Float, WebSocketSession? session = null)
    {
        dynamic? value = type switch
        {
            Output.TypeEnum.Float => _vr.GetFloatSetting(section, setting),
            Output.TypeEnum.Int32 => _vr.GetIntSetting(section, setting),
            Output.TypeEnum.Bool => _vr.GetBoolSetting(section, setting),
            Output.TypeEnum.String => _vr.GetStringSetting(section, setting),
            _ => null
        };
        var data = new JsonSetting(section, setting, value);
        SendCommandResult(requestKey, data, session);
    }

    private void SendEvent(EVREventType eventType, WebSocketSession? session = null)
    {
        var data = new JsonVREvent(eventType);
        SendResult(Response.CreateVREvent(data), session);
    }

    #endregion

    private void SendFoundOverlay(Request request, WebSocketSession? session = null)
    {
        DataFindOverlay? data = null;
        try
        {
            data = JsonSerializer.Deserialize<DataFindOverlay>(request.Data?.GetRawText() ?? "", JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
        }

        if (data != null)
        {
            var handle = _vr.FindOverlay(data.OverlayKey);
            var overlayResult = new JsonFindOverlay(data, handle);
            SendCommandResult(request.Key, overlayResult, session);
        }
        else SendResult(Response.CreateError("Input was invalid."), session); // TODO: Include response with shape of data?
    }

    private Response ApplyRemoteSetting(DataRemoteSetting data)
    {
        var errorResponse = CheckRemoteSetting(RequestKeyEnum.RemoteSetting, data.Password);
        if (errorResponse != null) return errorResponse;

        var settingSuccess = ApplySetting(data.Section, data.Setting, data.Value, data.Type);

        return !settingSuccess
            ? Response.CreateError($"Failed to set {data.Section}/{data.Setting} to {data.Value}.")
            : Response.CreateMessage($"Succeeded setting {data.Section}/{data.Setting} to {data.Value}.");
    }

    private Response ApplyMoveSpace(DataMoveSpace data)
    {
        var errorResponse = CheckRemoteSetting(RequestKeyEnum.MoveSpace, data.Password);
        if (errorResponse != null) return errorResponse;

        var newPos = new HmdVector3_t
        {
            v0 = data.OffsetX,
            v1 = data.OffsetY,
            v2 = data.OffsetZ
        };
        var success = _vr.MoveUniverse(newPos, data.MoveChaperone);
        return success
            ? Response.CreateMessage("Moved space successfully.")
            : Response.CreateError("Failed to move space.");
    }

    private Response? CheckRemoteSetting(RequestKeyEnum key, string password)
    {
        if (!_settings.RemoteSettings)
        {
            return Response.CreateError($"The command '{key}' relies on Remote Settings which is disabled. Enable it in the application interface.");
        }

        return password.Equals(_settings.RemoteSettingsPasswordHash)
            ? null
            : Response.CreateError("Password string did not match, b64-encode a binary SHA256 hash.");
    }

    private bool ApplySetting(string section, string setting, string value, TypeEnum type)
    {
        var boolSuccess = bool.TryParse(value, out var boolValue);
        var intSuccess = int.TryParse(value, out var intValue);
        var floatSuccess = float.TryParse(value, out var floatValue);
        if (type != TypeEnum.None)
        {
            switch (type)
            {
                case TypeEnum.String:
                    return _vr.SetStringSetting(section, setting, value);
                case TypeEnum.Bool:
                    return _vr.SetBoolSetting(section, setting, boolValue);
                case TypeEnum.Float:
                    return _vr.SetFloatSetting(section, setting, floatValue);
                case TypeEnum.Int32:
                    return _vr.SetIntSetting(section, setting, intValue);
                default:
                    return false;
            }
        }
        else
        {
            if (boolSuccess) return _vr.SetBoolSetting(section, setting, boolValue);
            if (intSuccess) return _vr.SetIntSetting(section, setting, intValue);
            if (floatSuccess) return _vr.SetFloatSetting(section, setting, floatValue);
            return _vr.SetStringSetting(section, setting, value);
        }
    }

    public async void Shutdown()
    {
        _openvrStatusAction += (_) => { };
        await _server.Stop();
        _shouldShutDown = true;
    }
}