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
using SuperSocket;
using SuperSocket.WebSocket.Server;
using Valve.VR;
using static EasyOpenVR.EasyOpenVRSingleton;

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
            var errorMessage = "Request was invalid";
            var request = new InputMessage();
            try
            {
                request = JsonSerializer.Deserialize<InputMessage>(message, JsonOptions.get());
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                Debug.WriteLine($"JSON Parsing Exception: {e.Message}");
            }

            if (request == null) SendResult(OutputMessage.CreateError($"Invalid command: {errorMessage}"), session);
            else HandleRequest(session, request); 
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
    private void SendCommandResult(InputMessageKeyEnum inputMessageKey, dynamic? data = null, string? nonce = null, WebSocketSession? session = null)
    {
        SendResult(OutputMessage.CreateCommand(inputMessageKey, data, nonce), session);
    }

    private void SendResult(OutputMessage outputMessage, WebSocketSession? session = null)
    {
        var jsonString = "";
        try
        {
            jsonString = JsonSerializer.Serialize(outputMessage, JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not serialize output for {outputMessage.Type}|{outputMessage.Key}: {e.Message}");
        }

        if (jsonString != string.Empty) _server.SendMessageToSingleOrAll(session, jsonString).DoNotAwait();
    }

    private void SendInput(OutputValueTypeEnum type, InputDigitalActionData_t data, InputActionInfo info, WebSocketSession? session = null)
    {
        var source = DataStore.handleToSource[info.sourceHandle];
        var json = new JsonInputDigital(source, data, info);
        SendResult(OutputMessage.Create(type, json), session);
    }

    private void HandleRequest(WebSocketSession? session, InputMessage inputMessage)
    {
        // Debug.WriteLine($"Command receieved: {Enum.GetName(typeof(CommandEnum), command.key)}");
        if (_stopRunning || !_vr.IsInitialized()) return;
        switch (inputMessage.Key)
        {
            case InputMessageKeyEnum.None: break;
            case InputMessageKeyEnum.CumulativeStats:
            {
                var stats = _vr.GetCumulativeStats();
                SendCommandResult(inputMessage.Key, new JsonCumulativeStats(stats), inputMessage.Nonce, session);
                break;
            }
            case InputMessageKeyEnum.PlayArea:
                SendPlayArea(inputMessage.Nonce, session);
                break;
            case InputMessageKeyEnum.ApplicationInfo:
                SendApplicationInfo(inputMessage.Nonce, session);
                break;
            case InputMessageKeyEnum.DeviceIds:
                DataStore.UpdateInputDeviceHandles();
                DataStore.UpdateDeviceIndices();
                SendDeviceIds(inputMessage.Nonce, session);
                break;
            case InputMessageKeyEnum.DeviceProperty:
            {
                SendDeviceProperty(inputMessage, session);
                break;
            }
            case InputMessageKeyEnum.InputAnalog:
                SendCommandResult(inputMessage.Key, DataStore.analogInputActionData, inputMessage.Nonce, session);
                break;
            case InputMessageKeyEnum.InputPose:
                SendCommandResult(inputMessage.Key, DataStore.poseInputActionData, inputMessage.Nonce, session);
                break;
            case InputMessageKeyEnum.Setting:
            {
                SendSetting(inputMessage, session);
                break;
            }
            case InputMessageKeyEnum.RemoteSetting:
            {
                SendRemoteSetting(inputMessage, session);
                break;
            }
            case InputMessageKeyEnum.FindOverlay:
            {
                SendFoundOverlay(inputMessage, session);
                break;
            }
            case InputMessageKeyEnum.MoveSpace:
            {
                SendMoveSpace(inputMessage, session);
                break;
            }
            case InputMessageKeyEnum.EditBindings:
            {
                SendEditBindings(inputMessage, session);
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
            SendInput(OutputValueTypeEnum.InputDigital, data, info);
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
            var deviceProperty = DataDeviceProperty.CreateFromEvent(data);
            SendDeviceProperty(InputMessageKeyEnum.DeviceProperty, deviceProperty);
        });
        _vr.RegisterEvent(EVREventType.VREvent_SteamVRSectionSettingChanged, (_) =>
        {
            // SendResult("Debug", data);
            var fakeData = new Dictionary<string, dynamic> { { "Issue", "https://github.com/ValveSoftware/openvr/issues/1335" } };
            SendCommandResult(InputMessageKeyEnum.Setting, fakeData);
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
        SendApplicationInfo(null, session);
        SendPlayArea(null, session);
        DataStore.UpdateInputDeviceHandles();
        DataStore.UpdateDeviceIndices();
        SendDeviceIds(null, session);
    }

    private void SendApplicationInfo(string? nonce = null, WebSocketSession? session = null)
    {
        var appId = _vr.GetRunningApplicationId();
        if (appId != _currentAppId)
        {
            _currentAppId = appId;
            _currentAppSessionTime = Utils.NowUnixUTC();
        }

        var json = new JsonApplicationInfo(_currentAppId, _currentAppSessionTime);
        SendCommandResult(InputMessageKeyEnum.ApplicationInfo, json, nonce, session);
    }

    private void SendPlayArea(string? nonce = null, WebSocketSession? session = null)
    {
        var rect = _vr.GetPlayAreaRect();
        var size = _vr.GetPlayAreaSize();
        var height = _vr.GetFloatSetting(OpenVR.k_pch_CollisionBounds_Section,
            OpenVR.k_pch_CollisionBounds_WallHeight_Float);
        SendCommandResult(InputMessageKeyEnum.PlayArea, new JsonPlayArea(rect, size, height), nonce, session);
    }

    private void SendDeviceIds(string? nonce = null, WebSocketSession? session = null)
    {
        var json = new JsonDeviceIds(DataStore.deviceToIndex.ToDictionary(), DataStore.sourceToIndex.ToDictionary());
        SendCommandResult(InputMessageKeyEnum.DeviceIds, json, nonce, session);
    }

    private void SendDeviceProperty(InputMessage inputMessage, WebSocketSession? session = null)
    {
        DataDeviceProperty? data = null;
        try
        {
            data = JsonSerializer.Deserialize<DataDeviceProperty>(inputMessage.Data?.GetRawText() ?? "", JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
        }

        if (data == null || data.Property == ETrackedDeviceProperty.Prop_Invalid)
        {
            SendResult(OutputMessage.CreateError($"Faulty property: {data?.Property}", new DataDeviceProperty(), inputMessage.Nonce), session);
            return;
        }

        SendDeviceProperty(inputMessage.Key, data, inputMessage.Nonce, session);
    }

    private void SendDeviceProperty(InputMessageKeyEnum key, DataDeviceProperty? data, string? nonce = null, WebSocketSession? session = null)
    {
        if (data == null || data.DeviceIndex == -1) {
            SendResult(OutputMessage.CreateError("The value of DeviceIndex was missing or -1 which means no valid device was specified.", null, nonce), session);
            return; 
        }
        var index = (uint)data.DeviceIndex;
        var propName = Enum.GetName(typeof(ETrackedDeviceProperty), data.Property);
        if (propName == null || data.Property == ETrackedDeviceProperty.Prop_Invalid)
        {
            SendResult(OutputMessage.CreateError($"The provided Property was invalid: {data.Property}.", null, nonce), session);
            return;
        }
        var propArray = propName.Split('_');
        var dataType = propArray.Last();
        var dataName = propArray.Length >= 1 ? propArray[1] : propName;
        var arrayType = dataType == "Array" ? propArray[^2] : string.Empty; // Matrix34, Int32, Float, Vector4, 
        Enum.TryParse(dataType, out OuputTypeEnum dataTypeEnum);
        dynamic? propertyValue = null;
        switch (dataTypeEnum)
        {
            case OuputTypeEnum.String:
                propertyValue = _vr.GetStringTrackedDeviceProperty(index, data.Property);
                break;
            case OuputTypeEnum.Bool:
                propertyValue = _vr.GetBooleanTrackedDeviceProperty(index, data.Property);
                break;
            case OuputTypeEnum.Float:
                propertyValue = _vr.GetFloatTrackedDeviceProperty(index, data.Property);
                break;
            case OuputTypeEnum.Matrix34:
                Debug.WriteLine($"{dataType} property: {propArray[1]}");
                break;
            case OuputTypeEnum.Uint64:
                propertyValue = _vr.GetLongTrackedDeviceProperty(index, data.Property);
                break;
            case OuputTypeEnum.Int32:
                propertyValue = _vr.GetIntegerTrackedDeviceProperty(index, data.Property);
                break;
            case OuputTypeEnum.Binary:
                Debug.WriteLine($"{dataType} property: {propArray[1]}");
                break;
            case OuputTypeEnum.Array:
                Debug.WriteLine($"{dataType}<{arrayType}> property: {propArray[1]}");
                break;
            case OuputTypeEnum.Vector3:
                Debug.WriteLine($"{dataType} property: {propArray[1]}");
                break;
            default:
                Debug.WriteLine($"{dataType} unhandled property: {propArray[1]}");
                break;
        }

        var json = new JsonDeviceProperty(data.DeviceIndex, dataName, propertyValue, dataType);
        SendCommandResult(key, json, nonce, session);
    }

    private void SendSetting(InputMessage inputMessage, WebSocketSession? session = null)
    {
        DataSetting? data = null;
        try
        {
            data = JsonSerializer.Deserialize<DataSetting>(inputMessage.Data?.GetRawText() ?? "", JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
        }

        if (data == null)
        {
            SendResult(OutputMessage.CreateError("Input was invalid, see Data as a reference.", new DataSetting(), inputMessage.Nonce), session);
            return;
        }

        dynamic? value = data.Type switch
        {
            OuputTypeEnum.Float => _vr.GetFloatSetting(data.Section, data.Setting),
            OuputTypeEnum.Int32 => _vr.GetIntSetting(data.Section, data.Setting),
            OuputTypeEnum.Bool => _vr.GetBoolSetting(data.Section, data.Setting),
            OuputTypeEnum.String => _vr.GetStringSetting(data.Section, data.Setting),
            _ => null
        };
        var json = new JsonSetting(data.Section, data.Setting, value);
        SendCommandResult(inputMessage.Key, json, inputMessage.Nonce, session);
    }

    private void SendRemoteSetting(InputMessage inputMessage, WebSocketSession? session)
    {
        DataRemoteSetting? data = null;
        try
        {
            data = JsonSerializer.Deserialize<DataRemoteSetting>(inputMessage.Data?.GetRawText() ?? "", JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
        }

        if (data != null)
        {
            var remoteSettingResponse = ApplyRemoteSetting(data, inputMessage);
            remoteSettingResponse.Key = inputMessage.Key;
            remoteSettingResponse.Nonce = inputMessage.Nonce;
            SendResult(remoteSettingResponse, session);
        }
        else SendResult(OutputMessage.CreateError("Input was invalid, see Data as a reference.", new DataRemoteSetting(), inputMessage.Nonce), session);
    }

    private void SendMoveSpace(InputMessage inputMessage, WebSocketSession? session)
    {
        DataMoveSpace? data = null;
        try
        {
            data = JsonSerializer.Deserialize<DataMoveSpace>(inputMessage.Data?.GetRawText() ?? "", JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
        }

        if (data != null)
        {
            ApplyMoveSpace(data, inputMessage, session);
        }
        else SendResult(OutputMessage.CreateError("Input was invalid, see Data as a reference.", DataMoveSpace.BuildEmpty(true), inputMessage.Nonce), session);
    }

    private void SendEditBindings(InputMessage inputMessage, WebSocketSession? session)
    {
        var error = OpenVR.Input.OpenBindingUI("", 0, 0, true);
        if (error != EVRInputError.None)
        {
            SendResult(OutputMessage.CreateError($"Failed to open bindings editor: {error}", null, inputMessage.Nonce), session);
            return;
        }
        var response = OutputMessage.CreateMessage("Opened bindings for application in editor.", inputMessage.Nonce);
        response.Key = inputMessage.Key;
        SendResult(response, session);
    }

    private void SendEvent(EVREventType eventType, WebSocketSession? session = null)
    {
        var json = new JsonVREvent(eventType);
        SendResult(OutputMessage.CreateVREvent(json), session);
    }

    #endregion

    private void SendFoundOverlay(InputMessage inputMessage, WebSocketSession? session = null)
    {
        DataFindOverlay? data = null;
        try
        {
            data = JsonSerializer.Deserialize<DataFindOverlay>(inputMessage.Data?.GetRawText() ?? "", JsonOptions.get());
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse incoming device property request: {e.Message}");
        }

        if (data != null)
        {
            var handle = _vr.FindOverlay(data.OverlayKey);
            var json = new JsonFindOverlay(data, handle);
            SendCommandResult(inputMessage.Key, json, inputMessage.Nonce, session);
        }
        else SendResult(OutputMessage.CreateError("Input was invalid, see Data as a reference.", new DataFindOverlay(), inputMessage.Nonce), session);
    }

    private OutputMessage ApplyRemoteSetting(DataRemoteSetting data, InputMessage inputMessage)
    {
        var errorResponse = CheckRemoteSetting(InputMessageKeyEnum.RemoteSetting, inputMessage.Password ?? "");
        if (errorResponse != null) return errorResponse;

        var settingSuccess = ApplySetting(data.Section, data.Setting, data.Value, data.Type);

        return !settingSuccess
            ? OutputMessage.CreateError($"Failed to set {data.Section}/{data.Setting} to {data.Value}.")
            : OutputMessage.CreateMessage($"Succeeded setting {data.Section}/{data.Setting} to {data.Value}.");
    }

    private void ApplyMoveSpace(DataMoveSpace data, InputMessage inputMessage, WebSocketSession? session = null)
    {
        var errorResponse = CheckRemoteSetting(InputMessageKeyEnum.MoveSpace, inputMessage.Password ?? "");
        if (errorResponse != null)
        {
                SendResult(errorResponse);
                return;
        }
        SpaceMover.MoveSpace(data, result =>
        {
            var response = OutputMessage.CreateMessage(result, inputMessage.Nonce);
            response.Key = inputMessage.Key;
            SendResult(response, session);
        });
    }

    private OutputMessage? CheckRemoteSetting(InputMessageKeyEnum key, string password)
    {
        if (!_settings.RemoteSettings)
        {
            return OutputMessage.CreateError($"The command '{key}' relies on Remote Settings which is disabled. Enable it in the application interface.");
        }

        return password.Equals(_settings.RemoteSettingsPasswordHash)
            ? null
            : OutputMessage.CreateError("Password string did not match, b64-encode a binary SHA256 hash.");
    }

    private bool ApplySetting(string section, string setting, string value, InputValueTypeEnum inputValueType)
    {
        var boolSuccess = bool.TryParse(value, out var boolValue);
        var intSuccess = int.TryParse(value, out var intValue);
        var floatSuccess = float.TryParse(value, out var floatValue);
        if (inputValueType != InputValueTypeEnum.None)
        {
            return inputValueType switch
            {
                InputValueTypeEnum.String => _vr.SetStringSetting(section, setting, value),
                InputValueTypeEnum.Bool => _vr.SetBoolSetting(section, setting, boolValue),
                InputValueTypeEnum.Float => _vr.SetFloatSetting(section, setting, floatValue),
                InputValueTypeEnum.Int32 => _vr.SetIntSetting(section, setting, intValue),
                _ => false
            };
        }
        if (boolSuccess) return _vr.SetBoolSetting(section, setting, boolValue);
        if (intSuccess) return _vr.SetIntSetting(section, setting, intValue);
        if (floatSuccess) return _vr.SetFloatSetting(section, setting, floatValue);
        return _vr.SetStringSetting(section, setting, value);
    }

    public async void Shutdown()
    {
        _openvrStatusAction += (_) => { };
        await _server.Stop();
        _shouldShutDown = true;
    }
}