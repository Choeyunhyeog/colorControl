using System.Runtime.InteropServices;
using System.Text;
using brightnessControl.Models;

namespace brightnessControl.Services;

internal sealed class NvApi
{
    private const int NvapiOk = 0;
    private const uint NvapiInitializeId = 0x0150e828;
    private const uint NvapiGetErrorMessageId = 0x6c2d048c;
    private const uint NvapiDispGetDisplayIdByDisplayNameId = 0xae457190;
    private const uint NvapiDispGetGdiPrimaryDisplayIdId = 0x1e9d8a31;
    private const uint NvapiGpuSetScanoutIntensityId = 0xa57457a4;
    private const uint NvapiGpuGetScanoutIntensityStateId = 0xe81ce836;

    private const uint ScanoutIntensityDataVersion = (32u | (2u << 16));
    private const uint ScanoutIntensityStateVersion = (8u | (1u << 16));

    private readonly AppLogger _logger;
    private readonly InitializeDelegate _initialize;
    private readonly GetErrorMessageDelegate _getErrorMessage;
    private readonly DispGetDisplayIdByDisplayNameDelegate _getDisplayIdByDisplayName;
    private readonly DispGetGdiPrimaryDisplayIdDelegate _getGdiPrimaryDisplayId;
    private readonly GpuSetScanoutIntensityDelegate _setScanoutIntensity;
    private readonly GpuGetScanoutIntensityStateDelegate _getScanoutIntensityState;
    private bool _initialized;

    public NvApi(AppLogger logger)
    {
        _logger = logger;

        _initialize = QueryInterface<InitializeDelegate>(NvapiInitializeId);
        _getErrorMessage = QueryInterface<GetErrorMessageDelegate>(NvapiGetErrorMessageId);
        _getDisplayIdByDisplayName =
            QueryInterface<DispGetDisplayIdByDisplayNameDelegate>(NvapiDispGetDisplayIdByDisplayNameId);
        _getGdiPrimaryDisplayId = QueryInterface<DispGetGdiPrimaryDisplayIdDelegate>(NvapiDispGetGdiPrimaryDisplayIdId);
        _setScanoutIntensity = QueryInterface<GpuSetScanoutIntensityDelegate>(NvapiGpuSetScanoutIntensityId);
        _getScanoutIntensityState = QueryInterface<GpuGetScanoutIntensityStateDelegate>(NvapiGpuGetScanoutIntensityStateId);

        var status = _initialize();
        if (status != NvapiOk)
        {
            throw new InvalidOperationException($"NvAPI_Initialize failed: {FormatStatus(status)}");
        }

        _initialized = true;
    }

    public uint GetDisplayId(DisplayTarget displayTarget)
    {
        EnsureInitialized();

        if (IsPrimaryTarget(displayTarget))
        {
            var primaryStatus = _getGdiPrimaryDisplayId(out var primaryDisplayId);
            if (primaryStatus != NvapiOk)
            {
                throw new InvalidOperationException(
                    $"NvAPI_DISP_GetGDIPrimaryDisplayId failed: {FormatStatus(primaryStatus)}");
            }

            return primaryDisplayId;
        }

        foreach (var deviceName in GetCandidateDisplayNames(displayTarget.DeviceName))
        {
            var status = _getDisplayIdByDisplayName(deviceName, out var displayId);
            if (status == NvapiOk)
            {
                return displayId;
            }
        }

        throw new InvalidOperationException(
            $"NvAPI_DISP_GetDisplayIdByDisplayName failed for {displayTarget.DeviceName}: no candidate display name matched.");
    }

    public void ApplyScanoutColorProfile(uint displayId, ColorProfile colorProfile)
    {
        EnsureInitialized();

        var brightness = Math.Clamp((colorProfile.BrightnessPercent - 100) / 100.0f, -1.0f, 1.0f);
        var contrast = Math.Clamp(colorProfile.ContrastPercent / 100.0f, 0.0f, 2.0f);
        var blendingTexture = new[] { contrast, contrast, contrast };
        var offsetTexture = new[] { brightness, brightness, brightness };

        unsafe
        {
            fixed (float* blendingPtr = blendingTexture)
            fixed (float* offsetPtr = offsetTexture)
            {
                var data = new ScanoutIntensityData
                {
                    Version = ScanoutIntensityDataVersion,
                    Width = 1,
                    Height = 1,
                    BlendingTexture = (IntPtr)blendingPtr,
                    OffsetTexture = (IntPtr)offsetPtr,
                    OffsetTexChannels = 3
                };

                var sticky = 0;
                var status = _setScanoutIntensity(displayId, ref data, ref sticky);
                if (status != NvapiOk)
                {
                    throw new InvalidOperationException(
                        $"NvAPI_GPU_SetScanoutIntensity failed for display 0x{displayId:X8}: {FormatStatus(status)}");
                }
            }
        }
    }

    public bool IsScanoutIntensityEnabled(uint displayId)
    {
        EnsureInitialized();

        var state = new ScanoutIntensityState
        {
            Version = ScanoutIntensityStateVersion
        };

        var status = _getScanoutIntensityState(displayId, ref state);
        if (status != NvapiOk)
        {
            throw new InvalidOperationException(
                $"NvAPI_GPU_GetScanoutIntensityState failed for display 0x{displayId:X8}: {FormatStatus(status)}");
        }

        return state.Enabled != 0;
    }

    public void ResetScanout(uint displayId)
    {
        EnsureInitialized();

        var blendingTexture = new[] { 1.0f, 1.0f, 1.0f };
        var offsetTexture = new[] { 0.0f, 0.0f, 0.0f };

        unsafe
        {
            fixed (float* blendingPtr = blendingTexture)
            fixed (float* offsetPtr = offsetTexture)
            {
                var data = new ScanoutIntensityData
                {
                    Version = ScanoutIntensityDataVersion,
                    Width = 1,
                    Height = 1,
                    BlendingTexture = (IntPtr)blendingPtr,
                    OffsetTexture = (IntPtr)offsetPtr,
                    OffsetTexChannels = 3
                };

                var sticky = 0;
                var status = _setScanoutIntensity(displayId, ref data, ref sticky);
                if (status != NvapiOk)
                {
                    _logger.Warning(
                        $"NvAPI neutral scanout reset failed for display 0x{displayId:X8}: {FormatStatus(status)}");
                }
            }
        }
    }

    public string FormatStatus(int status)
    {
        var builder = new StringBuilder(64);
        try
        {
            var errorStatus = _getErrorMessage(status, builder);
            return errorStatus == NvapiOk
                ? $"{builder} ({status})"
                : status.ToString();
        }
        catch
        {
            return status.ToString();
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("NvAPI is not initialized.");
        }
    }

    private static bool IsPrimaryTarget(DisplayTarget displayTarget)
    {
        return string.IsNullOrWhiteSpace(displayTarget.DeviceName) ||
               string.Equals(displayTarget.DeviceName.Trim(), "Primary", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetCandidateDisplayNames(string deviceName)
    {
        var trimmed = deviceName.Trim();
        yield return trimmed;

        if (trimmed.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            yield return trimmed[4..];
        }
        else
        {
            yield return $@"\\.\{trimmed}";
        }
    }

    private static T QueryInterface<T>(uint interfaceId)
        where T : Delegate
    {
        var pointer = nvapi_QueryInterface(interfaceId);
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"NvAPI function 0x{interfaceId:X8} is not available.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    [DllImport("nvapi64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nvapi_QueryInterface(uint interfaceId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetErrorMessageDelegate(int status, [Out] StringBuilder message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int DispGetDisplayIdByDisplayNameDelegate(
        string displayName,
        out uint displayId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DispGetGdiPrimaryDisplayIdDelegate(out uint displayId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GpuSetScanoutIntensityDelegate(
        uint displayId,
        ref ScanoutIntensityData scanoutIntensityData,
        ref int sticky);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GpuGetScanoutIntensityStateDelegate(
        uint displayId,
        ref ScanoutIntensityState scanoutIntensityStateData);

    [StructLayout(LayoutKind.Sequential)]
    private struct ScanoutIntensityData
    {
        public uint Version;
        public uint Width;
        public uint Height;
        public IntPtr BlendingTexture;
        public IntPtr OffsetTexture;
        public uint OffsetTexChannels;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScanoutIntensityState
    {
        public uint Version;
        public uint Enabled;
    }
}
