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
        if (exception is null)
        {
            Append("ERROR", message);
            return;
        }

        var details = new StringBuilder();
        details.Append(message);

        var current = exception;
        while (current is not null)
        {
            details.Append(" | ");
            details.Append(current.GetType().Name);
            details.Append(": ");
            details.Append(current.Message);
            current = current.InnerException;
        }

        if (exception.StackTrace is { Length: > 0 } stackTrace)
        {
            details.Append(" | StackTrace: ");
            details.Append(stackTrace.Replace(Environment.NewLine, " \\n "));
        }

        Append("ERROR", details.ToString());
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
