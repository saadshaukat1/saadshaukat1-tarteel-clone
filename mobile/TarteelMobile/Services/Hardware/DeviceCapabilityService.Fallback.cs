#if !WINDOWS && !ANDROID
using Microsoft.Extensions.Logging;

namespace TarteelMobile.Services.Hardware;

public sealed class DeviceCapabilityService : IDeviceCapabilityService
{
    private readonly ILogger<DeviceCapabilityService> _logger;
    private DeviceHardwareProfile? _cachedProfile;

    public DeviceCapabilityService(ILogger<DeviceCapabilityService> logger)
    {
        _logger = logger;
    }

    public DeviceHardwareProfile GetHardwareProfile()
    {
        if (_cachedProfile != null)
            return _cachedProfile;

        var logicalCores = Environment.ProcessorCount;
        var optimalThreads = Math.Clamp(logicalCores / 2, 2, 4);

        _cachedProfile = new DeviceHardwareProfile
        {
            TotalMemoryMegabytes = 8192,
            AvailableMemoryMegabytes = 4096,
            CpuLogicalCores = logicalCores,
            OptimalThreadCount = optimalThreads,
            Tier = HardwareTier.MidRange,
            HasGpuAcceleration = false,
            GpuDescription = "Generic CPU Fallback",
            RecommendedModelTier = "base",
            RecommendedPerformanceProfile = "Auto",
            DeviceSummary = $"Generic Environment | Cores: {logicalCores} | Assigned threads: {optimalThreads}"
        };

        return _cachedProfile;
    }
}
#endif
