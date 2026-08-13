namespace TarteelClone.Api;

public sealed record ApiStatus(bool IsReady, string Version, string Message);

public interface ILocalApi
{
    ApiStatus GetStatus();
}
