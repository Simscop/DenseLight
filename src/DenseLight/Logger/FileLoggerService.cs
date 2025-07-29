using DenseLight.Services;
using System.IO;

namespace DenseLight.Logger;

public class FileLoggerService : ILoggerService
{
    private readonly string _logFilePath;
    private readonly object _lock = new object();

    public FileLoggerService()
    {
        string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
    }

    public void LogInformation(string message) => Log("INFO", message);
    public void LogWarning(string message) => Log("WARN", message);
    public void LogError(string message) => Log("ERROR", message);
    public void LogDebug(string message) => Log("DEBUG", message);

    public void LogException(Exception ex, string message = null)
    {
        string logMessage = $"EXCEPTION: {message ?? "An exception occurred"} - {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
        Log("ERROR", logMessage);
    }

    private void Log(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 防止日志记录失败导致应用崩溃
        }
    }
}
