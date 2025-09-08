using System;
using System.IO;

public static class CameraWorkerLogger
{
    private static readonly object _lock = new object();
    private static string _logFilePath;
    private static string _prefix = string.Empty;
    
    static CameraWorkerLogger()
    {
        string logDirectory = Path.Combine(Environment.CurrentDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        _logFilePath = Path.Combine(logDirectory, $"CameraWorker_{processId}_{timestamp}.log");
        
        // Write initial header
        WriteToFile($"=== Camera Worker Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        WriteToFile($"Process ID: {processId}");
        WriteToFile($"Working Directory: {Environment.CurrentDirectory}");
        WriteToFile($"Log File: {_logFilePath}");
        WriteToFile($"=== End Header ===\n");
    }
    
    public static void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string prefix = string.IsNullOrWhiteSpace(_prefix) ? string.Empty : _prefix + " ";
        string logEntry = $"[{timestamp}] {prefix}{message}";
        
        // Write to console
        Console.WriteLine(logEntry);
        
        // Write to file
        WriteToFile(logEntry);
    }
    
    public static void LogError(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string prefix = string.IsNullOrWhiteSpace(_prefix) ? string.Empty : _prefix + " ";
        string logEntry = $"[{timestamp}] {prefix}ERROR: {message}";
        
        // Write to console
        Console.WriteLine(logEntry);
        
        // Write to file
        WriteToFile(logEntry);
    }
    
    public static void LogException(Exception ex, string context = "")
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string prefix = string.IsNullOrWhiteSpace(_prefix) ? string.Empty : _prefix + " ";
        string logEntry = $"[{timestamp}] {prefix}EXCEPTION {context}: {ex.Message}\n" +
                         $"                    Type: {ex.GetType().Name}\n" +
                         $"                    Stack: {ex.StackTrace}";
        
        if (ex.InnerException != null)
        {
            logEntry += $"\n                    Inner: {ex.InnerException.Message}";
        }
        
        // Write to console
        Console.WriteLine(logEntry);
        
        // Write to file
        WriteToFile(logEntry);
    }
    
    private static void WriteToFile(string content)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, content + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Fallback to console only if file writing fails
            Console.WriteLine($"[CameraWorkerLogger Error]: {ex.Message}");
        }
    }
    
    public static string GetLogFilePath()
    {
        return _logFilePath;
    }

    public static void SetPrefix(string prefix)
    {
        _prefix = prefix ?? string.Empty;
    }
}
