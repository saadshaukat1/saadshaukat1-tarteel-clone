using System.Net.Http.Json;
using TarteelMobile.Models;

namespace TarteelMobile.Services;

public interface IApiService
{
    string? AuthToken { get; }
    Task<bool>  LoginAsync(string email, string password);
    Task<bool>  RegisterAsync(string email, string password);
    Task<Verse?> GetVerseAsync(int surahNum, int ayahNum);
}

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://localhost:7001"; // override in prod

    public string? AuthToken { get; private set; }

    public ApiService()
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (result?.Token is null) return false;

        AuthToken = result.Token;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
        return true;
    }

    public async Task<bool> RegisterAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register",
            new { email, password });
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (result?.Token is null) return false;

        AuthToken = result.Token;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
        return true;
    }

    public async Task<Verse?> GetVerseAsync(int surahNum, int ayahNum)
        => await _http.GetFromJsonAsync<Verse>($"/api/quran/{surahNum}/{ayahNum}");

    private record TokenResponse(string Token);
}
