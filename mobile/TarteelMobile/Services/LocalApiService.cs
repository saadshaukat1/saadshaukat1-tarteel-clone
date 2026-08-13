using TarteelClone.Api;

namespace TarteelMobile.Services;

/// <summary>
/// Offline local API: reports readiness of the in-process services without a
/// network dependency. This is the real implementation behind ILocalApi —
/// the app never leaves offline mode.
/// </summary>
public sealed class LocalApiService : ILocalApi
{
    public const string Version = "1.0.0";

    public ApiStatus GetStatus()
        => new(true, Version, "Offline local API ready");
}
