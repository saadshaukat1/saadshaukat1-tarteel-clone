using System.Text;

namespace TarteelMobile.Services;

public interface IAppDiagnosticsService
{
    string LogPath { get; }
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
    Task<IReadOnlyList<string>> ReadRecentAsync(int maxLines = 200);
}

public sealed class FileAppDiagnosticsService : IAppDiagnosticsService
{
    private readonly object _sync = new();

    public FileAppDiagnosticsService()
    {
        var diagnosticsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TarteelClone",
            "diagnostics");

        Directory.CreateDirectory(diagnosticsDir);
        LogPath = Path.Combine(diagnosticsDir, "offline.log");
        Info("Diagnostics initialized.");
    }

    public string LogPath { get; }

    public void Info(string message) => Append("INFO", message);
    public void Warn(string message) => Append("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : $"{message} | {exception.GetType().Name}: {exception.Message}";

        Append("ERROR", fullMessage);
    }

    public async Task<IReadOnlyList<string>> ReadRecentAsync(int maxLines = 200)
    {
        if (!File.Exists(LogPath))
        {
            return [];
        }

        var allLines = await File.ReadAllLinesAsync(LogPath);
        if (allLines.Length <= maxLines)
        {
            return allLines;
        }

        return allLines.Skip(allLines.Length - maxLines).ToArray();
    }

    private void Append(string level, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:u} [{level}] {message}";
        lock (_sync)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
