using System;
using System.Runtime.InteropServices;
using ClassIsland.Services.VoiceControl.Abstractions;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 基于原生 Windows Core Audio API（MMDeviceEnumerator + IMMDevice + IAudioEndpointVolume）
/// 的系统默认播放设备音量/静音控制器。零 NuGet 依赖，仅 Windows 可用。
/// 所有方法都做了防御性 try/catch，失败时返回 false 而不是直接抛异常。
/// </summary>
public sealed class CoreAudioVolumeController : IDisposable
{
    private readonly IVoiceLogger _logger;
    private const float StepPercent = 10f; // 每次调音步进（0-100 标度）
    private IMMDeviceEnumerator? _enumerator;

    // MMDeviceEnumerator 的 CLSID（Windows 标准）。
    private static readonly Guid ClsIdMmDeviceEnumerator =
        new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

    public CoreAudioVolumeController(IVoiceLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        try
        {
            // .NET Core 下通过 CLSID 激活 COM coclass（不依赖 [CoClass] 设计时支持）。
            var type = Type.GetTypeFromCLSID(ClsIdMmDeviceEnumerator);
            if (type == null)
            {
                _logger.Error("无法解析 MMDeviceEnumerator CLSID（当前环境可能不支持 COM）。");
                return;
            }
            var instance = Activator.CreateInstance(type);
            _enumerator = (IMMDeviceEnumerator?)instance;
        }
        catch (Exception ex)
        {
            _logger.Error("初始化 Core Audio 枚举器失败（可能非 Windows 或缺少音频设备）", ex);
        }
    }

    public bool IsAvailable => _enumerator != null;

    private IAudioEndpointVolume? Endpoint
    {
        get
        {
            if (_enumerator == null) return null;
            try
            {
                _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
                if (device == null) return null;
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, CLSCTX.ALL, IntPtr.Zero, out var obj);
                return obj as IAudioEndpointVolume;
            }
            catch (Exception ex)
            {
                _logger.Error("获取音频端点音量接口失败", ex);
                return null;
            }
        }
    }

    public bool VolumeUp()
    {
        var ep = Endpoint;
        if (ep == null) return false;
        try
        {
            var cur = GetVolumePercent(ep);
            var next = Math.Min(100f, cur + StepPercent) / 100f;
            var ctx = Guid.Empty;
            ep.SetMasterVolumeLevelScalar(next, ref ctx);
            _logger.Info($"音量 +{StepPercent} => {next * 100:F0}");
            return true;
        }
        catch (Exception ex) { _logger.Error("调大音量失败", ex); return false; }
    }

    public bool VolumeDown()
    {
        var ep = Endpoint;
        if (ep == null) return false;
        try
        {
            var cur = GetVolumePercent(ep);
            var next = Math.Max(0f, cur - StepPercent) / 100f;
            var ctx = Guid.Empty;
            ep.SetMasterVolumeLevelScalar(next, ref ctx);
            _logger.Info($"音量 -{StepPercent} => {next * 100:F0}");
            return true;
        }
        catch (Exception ex) { _logger.Error("调小音量失败", ex); return false; }
    }

    public bool ToggleMute()
    {
        var ep = Endpoint;
        if (ep == null) return false;
        try
        {
            ep.GetMute(out var muted);
            var ctx = Guid.Empty;
            ep.SetMute(!muted, ref ctx);
            _logger.Info($"静音状态 => {!muted}");
            return true;
        }
        catch (Exception ex) { _logger.Error("切换静音失败", ex); return false; }
    }

    public bool Unmute()
    {
        var ep = Endpoint;
        if (ep == null) return false;
        try
        {
            var ctx = Guid.Empty;
            ep.SetMute(false, ref ctx);
            _logger.Info("已取消静音");
            return true;
        }
        catch (Exception ex) { _logger.Error("取消静音失败", ex); return false; }
    }

    public bool SetVolume(int level)
    {
        var ep = Endpoint;
        if (ep == null) return false;
        try
        {
            var v = Math.Clamp(level, 0, 100) / 100f;
            var ctx = Guid.Empty;
            ep.SetMasterVolumeLevelScalar(v, ref ctx);
            _logger.Info($"音量设置为 {v * 100:F0}");
            return true;
        }
        catch (Exception ex) { _logger.Error("设置音量失败", ex); return false; }
    }

    private static float GetVolumePercent(IAudioEndpointVolume ep)
    {
        ep.GetMasterVolumeLevelScalar(out var v);
        return v * 100f;
    }

    public void Dispose()
    {
        _enumerator = null;
        GC.SuppressFinalize(this);
    }
}

// ---- 原生 Windows Core Audio COM 互操作声明 ----

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    int GetState(out int pdwState);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr pNotify);
    int UnregisterControlChangeNotify(IntPtr pNotify);
    int GetChannelCount(out int pnChannelCount);
    int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    int GetMasterVolumeLevel(out float pfLevelDB);
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
    int VolumeStepUp(ref Guid pguidEventContext);
    int VolumeStepDown(ref Guid pguidEventContext);
    int QueryHardwareSupport(out uint pdwHardwareSupportMask);
    int GetVolumeRange(out float pflVolumeMinDB, out float pflVolumeMaxDB, out float pflVolumeIncrementDB);
}

internal enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
internal enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

[Flags]
internal enum CLSCTX : uint
{
    INPROC_SERVER = 0x1,
    INPROC_HANDLER = 0x2,
    LOCAL_SERVER = 0x4,
    REMOTE_SERVER = 0x10,
    ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
}
