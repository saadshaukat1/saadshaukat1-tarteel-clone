namespace TarteelMobile.Services.Hardware;

public enum HardwareTier
{
    /// <summary>
    /// Low-end mobile device (&lt; 4 GB RAM, budget SoC, or Android low-RAM device).
    /// Best with tiny-q5_1 or base-q5_1; timestamps disabled.
    /// </summary>
    LowEnd,

    /// <summary>
    /// Mid-range mobile device or entry PC (4 - 7 GB RAM, 6-8 cores).
    /// Best with base-q5_1 or small-q5_1; auto-gated timestamps.
    /// </summary>
    MidRange,

    /// <summary>
    /// High-end mobile device or standard PC (8 - 15 GB RAM, high-performance cores).
    /// Runs small-q5_1 or large-v3-turbo-q5_0 comfortably.
    /// </summary>
    HighEnd,

    /// <summary>
    /// Desktop workstation / gaming PC (>= 16 GB RAM, discrete GPU or NPU).
    /// Fully unconstrained: large-v3-turbo-q5_0 with full token timestamps.
    /// </summary>
    DesktopUltra
}

public sealed class DeviceHardwareProfile
{
    public long TotalMemoryMegabytes { get; init; }
    public long AvailableMemoryMegabytes { get; init; }
    public int CpuLogicalCores { get; init; }
    public int OptimalThreadCount { get; init; }
    public HardwareTier Tier { get; init; }
    public bool HasGpuAcceleration { get; init; }
    public string GpuDescription { get; init; } = "None / Unknown";
    public string RecommendedModelTier { get; init; } = "base";
    public string RecommendedPerformanceProfile { get; init; } = "Auto";
    public string DeviceSummary { get; init; } = string.Empty;
    public bool IsLowMemoryDevice => Tier == HardwareTier.LowEnd;
}

public interface IDeviceCapabilityService
{
    DeviceHardwareProfile GetHardwareProfile();
}
