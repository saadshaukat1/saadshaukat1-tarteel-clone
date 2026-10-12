#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace TarteelMobile.Services.Hardware;

public sealed class DeviceCapabilityService : IDeviceCapabilityService
{
    private readonly ILogger<DeviceCapabilityService> _logger;
    private DeviceHardwareProfile? _cachedProfile;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

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

        try
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                totalRamMb = (long)(memStatus.ullTotalPhys / (1024 * 1024));
                availRamMb = (long)(memStatus.ullAvailPhys / (1024 * 1024));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query GlobalMemoryStatusEx; defaulting to safe estimates.");
        }

        if (totalRamMb == 0)
        {
            // Fallback estimation if Win32 API fails
            totalRamMb = 8192;
            availRamMb = 4096;
        }

        var logicalCores = Environment.ProcessorCount;

        // On desktop x86/x64, hyperthreading doubles logical core count.
        // Whisper performs best utilizing physical cores (typically logical / 2)
        // capped between 4 and 8 threads to prevent L1/L2 cache trashing.
        int optimalThreads;
        if (logicalCores >= 16) optimalThreads = 6;
        else if (logicalCores >= 8) optimalThreads = 4;
        else if (logicalCores >= 4) optimalThreads = 3;
        else if (logicalCores >= 2) optimalThreads = 2;
        else optimalThreads = 1;

        HardwareTier tier;
        string recommendedModelTier;
        string recommendedPerformanceProfile;

        if (totalRamMb >= 14000)
        {
            // Workstation / Gaming PC: 16 GB+ RAM
            tier = HardwareTier.DesktopUltra;
            recommendedModelTier = "turbo"; // large-v3-turbo-q5_0 (547 MiB)
            recommendedPerformanceProfile = "Auto";
        }
        else if (totalRamMb >= 7000)
        {
            // Standard PC / Laptop: 8 - 15 GB RAM
            tier = HardwareTier.HighEnd;
            recommendedModelTier = "turbo";
            recommendedPerformanceProfile = "Auto";
        }
        else if (totalRamMb >= 3500)
        {
            // Entry-level PC: 4 - 7 GB RAM
            tier = HardwareTier.MidRange;
            recommendedModelTier = "base";
            recommendedPerformanceProfile = "Auto";
        }
        else
        {
            // Budget / Constrained PC: < 4 GB RAM
            tier = HardwareTier.LowEnd;
            recommendedModelTier = "tiny";
            recommendedPerformanceProfile = "Speed";
            optimalThreads = Math.Min(optimalThreads, 2);
        }

        // On Windows 10/11 x64, Vulkan / DirectCompute runtimes are broadly supported
        var hasGpu = true;
        var gpuDesc = "Windows D3D12 / Vulkan compatible GPU";

        var summary = $"Windows PC [{tier}] | RAM: {availRamMb}MB free / {totalRamMb}MB total | " +
                      $"CPU: {logicalCores} logical cores (assigned {optimalThreads} Whisper threads) | " +
                      $"GPU: {gpuDesc} | Default model: '{recommendedModelTier}'";

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

        _logger.LogInformation("Hardware capability profile detected: {Summary}", summary);
        return _cachedProfile;
    }
}
#endif
