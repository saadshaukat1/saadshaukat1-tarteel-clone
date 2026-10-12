#if ANDROID
using Android.App;
using Android.Content;
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

        long totalRamMb = 0;
        long availRamMb = 0;
        bool isLowRamDevice = false;

        try
        {
            var activityManager = Android.App.Application.Context.GetSystemService(Context.ActivityService) as ActivityManager;
            if (activityManager != null)
            {
                var memInfo = new ActivityManager.MemoryInfo();
                activityManager.GetMemoryInfo(memInfo);

                totalRamMb = memInfo.TotalMem / (1024 * 1024);
                availRamMb = memInfo.AvailMem / (1024 * 1024);
                isLowRamDevice = activityManager.IsLowRamDevice || memInfo.LowMemory;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Android ActivityManager memory info; using fallback.");
        }

        if (totalRamMb == 0)
        {
            totalRamMb = 4096;
            availRamMb = 2048;
        }

        var logicalCores = Environment.ProcessorCount;

        // Big.LITTLE / DynamIQ CPU optimization for Android ARM SoCs:
        // Modern Snapdragon, Exynos, Dimensity, and Tensor chips use a combination of
        // High-performance cores (Cortex-X/A78/A710) and Efficiency cores (Cortex-A55/A510).
        // Assigning threads to efficiency cores degrades performance due to thread barrier waits.
        // We limit threads to the performance cluster (typically 2 to 4 cores).
        int optimalThreads;
        if (logicalCores >= 8) optimalThreads = 4;
        else if (logicalCores >= 6) optimalThreads = 3;
        else if (logicalCores >= 4) optimalThreads = 3;
        else optimalThreads = 2;

        HardwareTier tier;
        string recommendedModelTier;
        string recommendedPerformanceProfile;

        if (totalRamMb >= 7500 && !isLowRamDevice)
        {
            // Flagship Android device (8GB+ RAM)
            tier = HardwareTier.HighEnd;
            recommendedModelTier = "small"; // small-q5_1 (182 MB) or turbo
            recommendedPerformanceProfile = "Auto";
        }
        else if (totalRamMb >= 3800 && !isLowRamDevice)
        {
            // Mid-range Android device (4GB - 7GB RAM)
            tier = HardwareTier.MidRange;
            recommendedModelTier = "base"; // base-q5_1 (57 MB)
            recommendedPerformanceProfile = "Auto";
        }
        else
        {
            // Budget / Android Go device (< 4GB RAM or low-memory flag)
            tier = HardwareTier.LowEnd;
            recommendedModelTier = "tiny"; // tiny-q5_1 (31 MB)
            recommendedPerformanceProfile = "Speed"; // Disable DTW timestamps to protect battery and FPS
            optimalThreads = 2;
        }

        var hasGpu = Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q; // Android 10+ has standard Vulkan 1.1 support
        var gpuDesc = hasGpu ? "Android Vulkan 1.1+ GPU compute ready" : "Android CPU-only fallback";

        var summary = $"Android [{tier}] | RAM: {availRamMb}MB free / {totalRamMb}MB total | " +
                      $"CPU: {logicalCores} cores (assigned {optimalThreads} Big-cluster threads) | " +
                      $"GPU: {gpuDesc} | Default model: '{recommendedModelTier}' (LowRam={isLowRamDevice})";

        _cachedProfile = new DeviceHardwareProfile
        {
            TotalMemoryMegabytes = totalRamMb,
            AvailableMemoryMegabytes = availRamMb,
            CpuLogicalCores = logicalCores,
            OptimalThreadCount = optimalThreads,
            Tier = tier,
            HasGpuAcceleration = hasGpu,
            GpuDescription = gpuDesc,
            RecommendedModelTier = recommendedModelTier,
            RecommendedPerformanceProfile = recommendedPerformanceProfile,
            DeviceSummary = summary
        };

        _logger.LogInformation("Android hardware capability profile detected: {Summary}", summary);
        return _cachedProfile;
    }
}
#endif
