using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FFmpeg.AutoGen.Example
{
    public static class FFmpegBinariesHelper
    {
        internal static void RegisterFFmpegBinaries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var current = Environment.CurrentDirectory;
                var probe = Path.Combine("FFmpeg", "bin");
                while (current != null)
                {
                    var ffmpegBinaryPath = Path.Combine(current, probe);
                    if (Directory.Exists(ffmpegBinaryPath))
                    {
                        Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                        ffmpeg.RootPath = ffmpegBinaryPath;
                        Console.WriteLine($"ffmpeg.RootPath set to: {ffmpeg.RootPath}");
                        return;
                    }
                    current = Directory.GetParent(current)?.FullName;
                }
                
                // Fallback - try to use system PATH
                ffmpeg.RootPath = Environment.CurrentDirectory;
                Console.WriteLine($"FFmpeg binaries not found under FFmpeg/bin; using current directory: {ffmpeg.RootPath}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ffmpeg.RootPath = "/usr/lib/x86_64-linux-gnu";
                Console.WriteLine($"ffmpeg.RootPath set to: {ffmpeg.RootPath}");
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
