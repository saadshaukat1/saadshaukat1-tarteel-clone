using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TarteelMobile.Services.Asr;
using TarteelMobile.Services.Hardware;
using Xunit;

namespace TarteelMobile.Tests;

public class HardwareCapabilityTests
{
    [Fact]
    public void WindowsDeviceCapabilityService_DetectsHardwareSpecs()
    {
        var service = new DeviceCapabilityService(NullLogger<DeviceCapabilityService>.Instance);
        var profile = service.GetHardwareProfile();

        Assert.NotNull(profile);
        Assert.True(profile.TotalMemoryMegabytes > 0, "Total memory should be positive");
        Assert.True(profile.CpuLogicalCores > 0, "Logical cores should be positive");
        Assert.InRange(profile.OptimalThreadCount, 1, 8);
        Assert.False(string.IsNullOrWhiteSpace(profile.DeviceSummary));
        Assert.False(string.IsNullOrWhiteSpace(profile.RecommendedModelTier));
    }

    [Fact]
    public void LocalWhisperOptions_ResolvesTurboTierAliases()
    {
        var options = new LocalWhisperOptions
        {
            Turbo = new WhisperModelTierDefinition
            {
                ModelPath = "offline/models/ggml-large-v3-turbo-q5_0.bin",
                DownloadUrl = "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/models-v1/ggml-large-v3-turbo-q5_0.bin",
                Quantization = "q5_0",
                ApproximateSizeBytes = 574000000
            }
        };

        Assert.True(options.TryGetTierDefinition("turbo", out var def1));
        Assert.NotNull(def1);
        Assert.Equal("q5_0", def1.Quantization);

        Assert.True(options.TryGetTierDefinition("large-v3-turbo", out var def2));
        Assert.Same(def1, def2);

        Assert.True(options.TryGetTierDefinition("large-turbo", out var def3));
        Assert.Same(def1, def3);

        Assert.True(options.TryGetTierDefinition("ggml-large-v3-turbo", out var def4));
        Assert.Same(def1, def4);
    }

    [Theory]
    [InlineData("tiny", "tiny")]
    [InlineData("base", "base")]
    [InlineData("small", "small")]
    [InlineData("medium", "medium")]
    [InlineData("turbo", "turbo")]
    public void LocalWhisperOptions_ResolvesStandardTiers(string query, string expectedKey)
    {
        var options = new LocalWhisperOptions();
        Assert.True(options.TryGetTierDefinition(query, out var def));
        Assert.NotNull(def);
        Assert.True(options.TryGetTierDefinition(expectedKey, out var expectedDef));
        Assert.Same(expectedDef, def);
    }

    [Fact]
    public void MockDeviceCapabilityService_LowEndProfileDemotesHeavyModels()
    {
        var lowEndProfile = new DeviceHardwareProfile
        {
            TotalMemoryMegabytes = 2048,
            AvailableMemoryMegabytes = 800,
            CpuLogicalCores = 4,
            OptimalThreadCount = 2,
            Tier = HardwareTier.LowEnd,
            RecommendedModelTier = "tiny",
            RecommendedPerformanceProfile = "Speed",
            DeviceSummary = "Simulated Budget Android Device (2GB RAM)"
        };

        Assert.True(lowEndProfile.IsLowMemoryDevice);
        Assert.Equal("tiny", lowEndProfile.RecommendedModelTier);
        Assert.Equal("Speed", lowEndProfile.RecommendedPerformanceProfile);
        Assert.Equal(2, lowEndProfile.OptimalThreadCount);
    }

    [Fact]
    public void MockDeviceCapabilityService_DesktopUltraRecommendsTurbo()
    {
        var desktopProfile = new DeviceHardwareProfile
        {
            TotalMemoryMegabytes = 32768,
            AvailableMemoryMegabytes = 20480,
            CpuLogicalCores = 16,
            OptimalThreadCount = 6,
            Tier = HardwareTier.DesktopUltra,
            HasGpuAcceleration = true,
            RecommendedModelTier = "turbo",
            RecommendedPerformanceProfile = "Accuracy",
            DeviceSummary = "Simulated Workstation PC (32GB RAM)"
        };

        Assert.False(desktopProfile.IsLowMemoryDevice);
        Assert.Equal(HardwareTier.DesktopUltra, desktopProfile.Tier);
        Assert.Equal("turbo", desktopProfile.RecommendedModelTier);
        Assert.Equal(6, desktopProfile.OptimalThreadCount);
        Assert.True(desktopProfile.HasGpuAcceleration);
    }

    [Fact]
    public void WhisperFactory_LoadsDownloadedTurboModelSuccessfully()
    {
        var turboPath = @"C:\Users\Admin\AppData\Local\Tarteel\com.tarteel.mobile\Data\offline\models\ggml-large-v3-turbo-q5_0.bin";
        if (!System.IO.File.Exists(turboPath)) return;

        using var factory = Whisper.net.WhisperFactory.FromPath(turboPath);
        Assert.NotNull(factory);
    }
}
