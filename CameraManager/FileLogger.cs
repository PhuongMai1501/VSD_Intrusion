using System;
using System.IO;

namespace CameraManager
{
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static volatile bool _isShuttingDown = false; // Flag để đánh dấu trạng thái shutdown

        static FileLogger()
        {
            string logDirectory = Path.Combine(Environment.CurrentDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmms");
            _logFilePath = Path.Combine(logDirectory, $"CameraManager_{timestamp}.log");

            // Write initial header
            WriteToFile($"=== Camera Manager Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            WriteToFile($"Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
            WriteToFile($"Working Directory: {Environment.CurrentDirectory}");
            WriteToFile($"Log File: {_logFilePath}");
            WriteToFile("=== End Header ===\n");
        }

        public static void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] {message}";

            // Safe console write - tránh lỗi handle invalid trong shutdown
            if (!_isShuttingDown)
            {
                try
                {
                    Console.WriteLine(logEntry);
                }
                catch (IOException)
                {
                    _isShuttingDown = true;
                }
                catch (Exception)
                {
                    _isShuttingDown = true;
                }
            }

            // Always write to file
            WriteToFile(logEntry);
        }

        public static void LogError(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] ERROR: {message}";

            if (!_isShuttingDown)
            {
                try
                {
                    Console.WriteLine(logEntry);
                }
                catch (IOException)
                {
                    _isShuttingDown = true;
                }
                catch (Exception)
                {
                    _isShuttingDown = true;
                }
            }

            WriteToFile(logEntry);
        }

        public static void LogException(Exception ex, string context = "")
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] EXCEPTION {context}: {ex.Message}\n" +
                             $"                    Type: {ex.GetType().Name}\n" +
                             $"                    Stack: {ex.StackTrace}";

            if (ex.InnerException != null)
            {
                logEntry += $"\n                    Inner: {ex.InnerException.Message}";
            }

            if (!_isShuttingDown)
            {
                try
                {
                    Console.WriteLine(logEntry);
                }
                catch (IOException)
                {
                    _isShuttingDown = true;
                }
                catch (Exception)
                {
                    _isShuttingDown = true;
                }
            }

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
                if (!_isShuttingDown)
                {
                    try
                    {
                        Console.WriteLine($"[FileLogger Error]: {ex.Message}");
                    }
                    catch
                    {
                        // Ignore console failures
                    }
                }
            }
        }

        public static string GetLogFilePath()
        {
            return _logFilePath;
        }

        // Đánh dấu trạng thái shutdown để tránh ghi console
        public static void MarkShutdown()
        {
            _isShuttingDown = true;
            WriteToFile("=== FileLogger: Shutdown mode activated - Console output disabled ===");
        }
    }
}
