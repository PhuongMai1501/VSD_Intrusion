using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using FFmpeg.AutoGen;
using FFmpeg.AutoGen.Example;

public unsafe class Program
{
    private static bool _shouldExit = false;
    
    public static void Main(string[] args)
    {
        var startTime = DateTime.Now;
        CameraWorkerLogger.Log($"=== CameraWorker Started ===");
        CameraWorkerLogger.Log($"Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
        
        if (args.Length < 3)
        {
            CameraWorkerLogger.LogError("Insufficient arguments. Usage: CameraWorker <rtsp_url> <mmf_name> <mutex_name>");
            Environment.Exit(1);
        }

        string rtspUrl = args[0];
        string mmfName = args[1];
        string mutexName = args[2];
        string cameraName = (args.Length >= 4 && !string.IsNullOrWhiteSpace(args[3])) ? args[3] : null;
        int stt = 0;
        if (args.Length >= 5) int.TryParse(args[4], out stt);
        string connStr = (args.Length >= 6) ? args[5] : null;
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            // Derive simple name from mmfName if possible
            cameraName = mmfName;
        }

        CameraWorkerLogger.SetPrefix(cameraName);
        CameraWorkerLogger.Log($"RTSP URL: {rtspUrl}");
        CameraWorkerLogger.Log($"MMF Name: {mmfName}");
        CameraWorkerLogger.Log($"Mutex Name: {mutexName}");

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            _shouldExit = true;
            CameraWorkerLogger.Log("Received exit signal, shutting down gracefully...");
        };

        // Handle process termination signals
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => {
            _shouldExit = true;
            CameraWorkerLogger.Log("Process exit event received, shutting down...");
        };

        try
        {
            // Register FFmpeg binaries
            CameraWorkerLogger.Log("Registering FFmpeg binaries...");
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
            CameraWorkerLogger.Log("✅ FFmpeg binaries registered");
            try { ffmpeg.avformat_network_init(); } catch { }

            // Auto-reconnect loop
            int attempt = 0;
            while (!_shouldExit)
            {
                try
                {
                    attempt++;
                    // Reload RTSP from DB if info available
                    string attemptRtsp = ResolveRtspUrl(rtspUrl, stt, connStr) ?? rtspUrl;
                    if (!string.Equals(attemptRtsp, rtspUrl, StringComparison.Ordinal))
                    {
                        CameraWorkerLogger.Log($"Reloaded RTSP from DB (STT={stt})");
                    }
                    CameraWorkerLogger.Log($"Connect attempt #{attempt} -> {attemptRtsp}");
                    // Start processing; returns on error or natural end
                    ProcessRTSPStream(attemptRtsp, mmfName, mutexName);
                }
                catch (Exception ex)
                {
                    CameraWorkerLogger.LogException(ex, "Main/Loop");
                }

                if (_shouldExit) break;
                CameraWorkerLogger.Log("⚠️ Stream ended or failed. Reconnecting in 5 seconds...");
                Thread.Sleep(5000);
            }
        }
        catch (Exception ex)
        {
            CameraWorkerLogger.LogException(ex, "Main");
            Environment.Exit(1);
        }

        CameraWorkerLogger.Log("CameraWorker exiting normally");
    }

    private static void ProcessRTSPStream(string rtspUrl, string mmfName, string mutexName)
    {
        AVFormatContext* pFormatContext = null;
        AVCodecContext* pCodecContext = null;
        SwsContext* pSwsContext = null;
        AVPacket* pPacket = null;
        AVFrame* pFrame = null;
        AVFrame* pFrameRGB = null;

        try
        {
            CameraWorkerLogger.Log("=== Starting RTSP Stream Processing ===");

            // 1. Open RTSP stream
            CameraWorkerLogger.Log("Step 1: Opening RTSP stream...");
            pFormatContext = ffmpeg.avformat_alloc_context();
            if (pFormatContext == null)
            {
                throw new Exception("Failed to allocate format context");
            }

            AVDictionary* options = null;
            try
            {
                ffmpeg.av_dict_set(&options, "rtsp_transport", "tcp", 0);
                ffmpeg.av_dict_set(&options, "stimeout", "5000000", 0); // 5 second timeout
                ffmpeg.av_dict_set(&options, "rw_timeout", "5000000", 0); // 5 second IO timeout
                ffmpeg.av_dict_set(&options, "probesize", "1000000", 0);
                ffmpeg.av_dict_set(&options, "analyzeduration", "1000000", 0);
                ffmpeg.av_dict_set(&options, "max_delay", "250000", 0); // 0.25 seconds
                ffmpeg.av_dict_set(&options, "fflags", "nobuffer", 0);

                int openResult = ffmpeg.avformat_open_input(&pFormatContext, rtspUrl, null, &options);
                if (openResult < 0)
                {
                    throw new Exception($"Cannot open RTSP stream: {FFmpegHelper.av_strerror(openResult)}");
                }
                CameraWorkerLogger.Log("✅ RTSP stream opened successfully");
            }
            finally
            {
                if (options != null)
                    ffmpeg.av_dict_free(&options);
            }

            // 2. Find stream info
            CameraWorkerLogger.Log("Step 2: Finding stream info...");
            int streamInfoResult = ffmpeg.avformat_find_stream_info(pFormatContext, null);
            if (streamInfoResult < 0)
            {
                throw new Exception($"Cannot find stream info: {FFmpegHelper.av_strerror(streamInfoResult)}");
            }
            CameraWorkerLogger.Log($"✅ Found {pFormatContext->nb_streams} streams");

            // 3. Find video stream
            CameraWorkerLogger.Log("Step 3: Finding video stream...");
            AVCodec* pCodec = null;
            int videoStreamIndex = ffmpeg.av_find_best_stream(pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &pCodec, 0);
            if (videoStreamIndex < 0)
            {
                throw new Exception("No video stream found");
            }

            var codecpar = pFormatContext->streams[videoStreamIndex]->codecpar;
            CameraWorkerLogger.Log($"✅ Video stream found: {codecpar->width}x{codecpar->height}, codec: {codecpar->codec_id}");

            // 4. Setup decoder
            CameraWorkerLogger.Log("Step 4: Setting up decoder...");
            if (pCodec == null)
            {
                pCodec = ffmpeg.avcodec_find_decoder(codecpar->codec_id);
            }
            if (pCodec == null)
            {
                throw new Exception($"Decoder not found for codec: {codecpar->codec_id}");
            }

            string codecName = System.Text.Encoding.UTF8.GetString(pCodec->name, 50).TrimEnd('\0');
            CameraWorkerLogger.Log($"✅ Using decoder: {codecName}");

            pCodecContext = ffmpeg.avcodec_alloc_context3(pCodec);
            if (pCodecContext == null)
            {
                throw new Exception("Failed to allocate codec context");
            }

            ffmpeg.avcodec_parameters_to_context(pCodecContext, codecpar);
            
            int codecOpenResult = ffmpeg.avcodec_open2(pCodecContext, pCodec, null);
            if (codecOpenResult < 0)
            {
                throw new Exception($"Cannot open codec: {FFmpegHelper.av_strerror(codecOpenResult)}");
            }

            int width = pCodecContext->width;
            int height = pCodecContext->height;
            CameraWorkerLogger.Log($"✅ Decoder opened: {width}x{height}");

            // 5. Setup frame conversion (YUV to BGR24)
            CameraWorkerLogger.Log("Step 5: Setting up YUV to BGR24 frame conversion...");
            pSwsContext = ffmpeg.sws_getContext(
                width, height, pCodecContext->pix_fmt,  // Source: YUV format from decoder
                width, height, AVPixelFormat.AV_PIX_FMT_BGR24,  // Target: BGR24 for Windows
                ffmpeg.SWS_BILINEAR, null, null, null);
                
            if (pSwsContext == null)
            {
                throw new Exception("Failed to create SWS context for YUV to BGR24 conversion");
            }
            CameraWorkerLogger.Log("✅ SWS context created for YUV to BGR24 conversion");

            pFrameRGB = ffmpeg.av_frame_alloc();
            if (pFrameRGB == null)
            {
                throw new Exception("Failed to allocate RGB frame");
            }

            // Calculate BGR24 buffer size
            int rgbBufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, width, height, 1);
            byte* rgbBuffer = (byte*)ffmpeg.av_malloc((ulong)rgbBufferSize);
            if (rgbBuffer == null)
            {
                throw new Exception("Failed to allocate RGB buffer");
            }

            // Create data and linesize arrays for RGB frame
            var rgbData = new byte_ptrArray4();
            var rgbLinesize = new int_array4();
            ffmpeg.av_image_fill_arrays(ref rgbData, ref rgbLinesize, rgbBuffer, AVPixelFormat.AV_PIX_FMT_BGR24, width, height, 1);
            
            // Copy to frame structure
            pFrameRGB->data[0] = rgbData[0];
            pFrameRGB->linesize[0] = rgbLinesize[0];
            
            CameraWorkerLogger.Log($"✅ RGB frame setup complete: {width}x{height}, buffer size: {rgbBufferSize} bytes");

            // 6. Open MMF and Mutex
            CameraWorkerLogger.Log("Step 6: Opening MMF and Mutex...");
            using var mmf = MemoryMappedFile.OpenExisting(mmfName);
            using var accessor = mmf.CreateViewAccessor();
            using var mutex = Mutex.OpenExisting(mutexName);
            CameraWorkerLogger.Log("✅ MMF and Mutex opened successfully");

            // Write frame dimensions to MMF header
            mutex.WaitOne();
            try
            {
                accessor.Write(0, width);   // First 4 bytes: width
                accessor.Write(4, height);  // Next 4 bytes: height
                CameraWorkerLogger.Log($"✅ Frame dimensions ({width}x{height}) written to MMF header");
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            // 7. Allocate frame and packet
            pPacket = ffmpeg.av_packet_alloc();
            pFrame = ffmpeg.av_frame_alloc();
            if (pPacket == null || pFrame == null)
            {
                throw new Exception("Failed to allocate packet or frame");
            }

            CameraWorkerLogger.Log("✅ Starting main decode loop...");
            
            int frameCount = 0;
            DateTime lastLogTime = DateTime.Now;

            // 8. Main decode loop
            while (!_shouldExit && ffmpeg.av_read_frame(pFormatContext, pPacket) >= 0)
            {
                // Check for exit signal frequently
                if (_shouldExit)
                {
                    CameraWorkerLogger.Log("Exit signal received during decode loop, breaking...");
                    break;
                }

                try
                {
                    if (pPacket->stream_index == videoStreamIndex)
                    {
                        // Send packet to decoder
                        int sendResult = ffmpeg.avcodec_send_packet(pCodecContext, pPacket);
                        if (sendResult < 0)
                        {
                            if (sendResult != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                            {
                                CameraWorkerLogger.Log($"⚠️ Error sending packet: {FFmpegHelper.av_strerror(sendResult)}");
                            }
                            continue;
                        }

                        // Receive frames from decoder
                        while (!_shouldExit)
                        {
                            int receiveResult = ffmpeg.avcodec_receive_frame(pCodecContext, pFrame);
                            
                            if (receiveResult == ffmpeg.AVERROR(ffmpeg.EAGAIN) || receiveResult == ffmpeg.AVERROR_EOF)
                            {
                                break; // Need more packets or end of stream
                            }
                            
                            if (receiveResult < 0)
                            {
                                CameraWorkerLogger.Log($"⚠️ Error receiving frame: {FFmpegHelper.av_strerror(receiveResult)}");
                                break;
                            }

                            frameCount++;

                            // Convert YUV frame to BGR24 using sws_scale
                            int scaleResult = ffmpeg.sws_scale(pSwsContext,
                                pFrame->data, pFrame->linesize, 0, height,
                                pFrameRGB->data, pFrameRGB->linesize);
                                
                            if (scaleResult != height)
                            {
                                CameraWorkerLogger.Log($"⚠️ sws_scale returned {scaleResult}, expected {height}");
                                continue;
                            }

                            // 🔧 REMOVED: Default blue rectangle overlay - now handled by AI detection in CameraManager
                            // DrawBlueRectangleOverlay(pFrameRGB, width, height);

                            // Write BGR24 frame to MMF
                            byte* destBufferPtr = null;
                            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destBufferPtr);
                            
                            try
                            {
                                if (mutex.WaitOne(10)) // 10ms timeout
                                {
                                    try
                                    {
                                        // Skip the 8-byte header (width + height)
                                        byte* frameDataPtr = destBufferPtr + 8;
                                        long frameSize = (long)width * height * 3; // BGR24 = 3 bytes per pixel
                                        
                                        // Copy BGR24 data line by line to handle stride differences
                                        byte* srcData = pFrameRGB->data[0];
                                        int srcLineSize = pFrameRGB->linesize[0];
                                        int dstLineSize = width * 3; // BGR24 packed format
                                        
                                        for (int y = 0; y < height; y++)
                                        {
                                            byte* srcLine = srcData + (y * srcLineSize);
                                            byte* dstLine = frameDataPtr + (y * dstLineSize);
                                            Buffer.MemoryCopy(srcLine, dstLine, dstLineSize, dstLineSize);
                                        }
                                        
                                        // Log first successful frame write
                                        if (frameCount == 1)
                                        {
                                            CameraWorkerLogger.Log($"✅ First BGR24 frame written to MMF: {width}x{height}, size: {frameSize} bytes");
                                        }
                                    }
                                    finally
                                    {
                                        mutex.ReleaseMutex();
                                    }
                                }
                            }
                            finally
                            {
                                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                            }

                            // Log progress every 30 frames or 5 seconds
                            if (frameCount % 30 == 0 || (DateTime.Now - lastLogTime).TotalSeconds >= 5)
                            {
                                CameraWorkerLogger.Log($"✅ Processed {frameCount} frames (YUV→BGR24→MMF)");
                                lastLogTime = DateTime.Now;
                            }
                        }
                    }
                }
                finally
                {
                    ffmpeg.av_packet_unref(pPacket);
                }
            }

            if (_shouldExit)
            {
                CameraWorkerLogger.Log($"Decode loop exited due to shutdown signal. Total frames processed: {frameCount}");
            }
            else
            {
                CameraWorkerLogger.Log($"Decode loop ended naturally. Total frames processed: {frameCount}");
            }
            
            if (rgbBuffer != null)
            {
                ffmpeg.av_free(rgbBuffer);
            }
        }
        catch (Exception ex)
        {
            CameraWorkerLogger.LogException(ex, "ProcessRTSPStream");
        }
        finally
        {
            // Cleanup resources
            CameraWorkerLogger.Log("Cleaning up resources...");
            
            if (pSwsContext != null)
            {
                ffmpeg.sws_freeContext(pSwsContext);
            }
            
            if (pFrameRGB != null)
            {
                ffmpeg.av_frame_free(&pFrameRGB);
            }
            
            if (pFrame != null)
            {
                ffmpeg.av_frame_free(&pFrame);
            }
            
            if (pPacket != null)
            {
                ffmpeg.av_packet_free(&pPacket);
            }
            
            if (pCodecContext != null)
            {
                ffmpeg.avcodec_free_context(&pCodecContext);
            }
            
            if (pFormatContext != null)
            {
                ffmpeg.avformat_close_input(&pFormatContext);
            }

            CameraWorkerLogger.Log("✅ Resources cleaned up");
        }
    }

    private static string ResolveRtspUrl(string currentRtsp, int stt, string connStr)
    {
        try
        {
            if (stt <= 0 || string.IsNullOrWhiteSpace(connStr)) return currentRtsp;

            // Try load MySql.Data dynamically
            var asm = LoadAssemblySafe("MySql.Data")
                      ?? LoadAssemblySafe(Path.Combine(Environment.CurrentDirectory, "MySql.Data.dll"));
            if (asm == null)
            {
                CameraWorkerLogger.Log("MySql.Data not found; using last RTSP");
                return currentRtsp;
            }

            var connType = asm.GetType("MySql.Data.MySqlClient.MySqlConnection");
            var cmdType = asm.GetType("MySql.Data.MySqlClient.MySqlCommand");
            if (connType == null || cmdType == null)
            {
                CameraWorkerLogger.Log("MySql types not found; using last RTSP");
                return currentRtsp;
            }

            using var conn = (IDisposable)Activator.CreateInstance(connType, new object[] { connStr });
            connType.GetMethod("Open")?.Invoke(conn, null);

            string sql = $"SELECT RTSP_URL FROM camera_list WHERE STT = {stt} LIMIT 1";
            using var cmd = (IDisposable)Activator.CreateInstance(cmdType, new object[] { sql, conn });
            var execReader = cmdType.GetMethod("ExecuteReader", Type.EmptyTypes);
            var reader = execReader?.Invoke(cmd, null);
            if (reader == null) { connType.GetMethod("Close")?.Invoke(conn, null); return currentRtsp; }

            var readerType = reader.GetType();
            bool hasRow = (bool)(readerType.GetMethod("Read")?.Invoke(reader, null) ?? false);
            string url = null;
            if (hasRow)
            {
                url = (string)readerType.GetMethod("GetString", new Type[] { typeof(int) })?.Invoke(reader, new object[] { 0 });
                url = url?.Trim();
            }
            readerType.GetMethod("Close")?.Invoke(reader, null);
            connType.GetMethod("Close")?.Invoke(conn, null);

            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
            return currentRtsp;
        }
        catch (Exception ex)
        {
            CameraWorkerLogger.LogException(ex, "ResolveRtspUrl");
            return currentRtsp;
        }
    }

    private static System.Reflection.Assembly LoadAssemblySafe(string nameOrPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nameOrPath)) return null;
            if (File.Exists(nameOrPath)) return System.Reflection.Assembly.LoadFrom(nameOrPath);
            return System.Reflection.Assembly.Load(nameOrPath);
        }
        catch { return null; }
    }

    private static void DrawBlueRectangleOverlay(AVFrame* pFrameRGB, int width, int height)
    {
        // Draw a green rectangle border overlay in the center of the frame
        // Rectangle size: 100x100 pixels, border thickness: 4 pixels

        int rectWidth = 100;
        int rectHeight = 100;
        int centerX = (width - rectWidth) / 2;
        int centerY = (height - rectHeight) / 2;
        int borderThickness = 4;

        byte* frameData = pFrameRGB->data[0];
        int lineSize = pFrameRGB->linesize[0];

        // Green color values in BGR24 format
        byte blueChannel = 0;    // B
        byte greenChannel = 255; // G  
        byte redChannel = 0;     // R

        // Draw rectangle border (outline only)
        for (int y = centerY; y < centerY + rectHeight; y++)
        {
            if (y < 0 || y >= height) continue;

            for (int x = centerX; x < centerX + rectWidth; x++)
            {
                if (x < 0 || x >= width) continue;

                // Check if current pixel is on the border
                bool isTopBorder = (y >= centerY && y < centerY + borderThickness);
                bool isBottomBorder = (y >= centerY + rectHeight - borderThickness && y < centerY + rectHeight);
                bool isLeftBorder = (x >= centerX && x < centerX + borderThickness);
                bool isRightBorder = (x >= centerX + rectWidth - borderThickness && x < centerX + rectWidth);

                // Only draw if pixel is on the border
                if (isTopBorder || isBottomBorder || isLeftBorder || isRightBorder)
                {
                    // Calculate pixel position in frame buffer
                    int pixelOffset = y * lineSize + x * 3;

                    // Set BGR color values for green
                    frameData[pixelOffset + 0] = blueChannel;  // B
                    frameData[pixelOffset + 1] = greenChannel; // G
                    frameData[pixelOffset + 2] = redChannel;   // R
                }
            }
        }
    }
}
