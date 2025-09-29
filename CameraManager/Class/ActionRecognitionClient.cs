using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
namespace CameraManager.Class
{
    public class ActionRecognitionClient : IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _apiUrl;

        public ActionRecognitionClient(string baseUrl)
        {
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 1, // giữ 1 kết nối/Client để sticky 1 worker
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            _client = new HttpClient(handler)
            {
                // Nâng timeout nhẹ để giảm TaskCanceledException khi API hơi chậm
                Timeout = TimeSpan.FromSeconds(3.5)
            };
            // Endpoint mục tiêu là /detect
            _apiUrl = $"{baseUrl.TrimEnd('/')}/detect";
        }

        public async Task<MultiPersonDetectionResponse> DetectAsync(string base64Image, string streamId = null)
        {
            if (string.IsNullOrEmpty(base64Image))
            {
                return null;
            }

            // Tạo nội dung JSON, key là "frame" và đính kèm stream_id nếu có
            string jsonPayload = streamId == null
                ? $"{{\"frame\": \"{base64Image}\"}}"
                : $"{{\"frame\": \"{base64Image}\", \"stream_id\": \"{streamId}\"}}";
            int maxAttempts = 2; // 1 retry nhẹ
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                    {
                        Content = content
                    };
                    request.Version = HttpVersion.Version11; // ưu tiên giữ connection HTTP/1.1
                    if (!string.IsNullOrEmpty(streamId))
                    {
                        request.Headers.TryAddWithoutValidation("X-Stream-ID", streamId);
                    }

                    // Đọc sớm headers để giảm thời gian giữ kết nối chờ body
                    var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return JsonConvert.DeserializeObject<MultiPersonDetectionResponse>(responseJson);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Lỗi HTTP: {response.StatusCode}. Phản hồi: {responseJson}");
                        // Không retry cho mã lỗi HTTP cụ thể; chỉ retry khi exception/timeout
                        return null;
                    }
                }
                catch (TaskCanceledException tce)
                {
                    // Thường do timeout; thử retry 1 lần
                    if (attempt < maxAttempts)
                    {
                        Console.WriteLine("⏳ Timeout khi gọi API, retry nhẹ...");
                        await Task.Delay(150);
                        continue;
                    }
                    Console.WriteLine($"💥 Timeout khi gọi API (hết retry): {tce.Message}");
                    return null;
                }
                catch (HttpRequestException hre)
                {
                    if (attempt < maxAttempts)
                    {
                        Console.WriteLine($"⚠️ HttpRequestException (attempt {attempt}) => retry nhẹ: {hre.Message}");
                        await Task.Delay(150);
                        continue;
                    }
                    Console.WriteLine($"💥 HttpRequestException (hết retry): {hre.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"💥 Exception khi gọi API: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        // Hàm để reset buffer trên server khi cần
        public async Task ResetServerBuffer()
        {
            string resetUrl = _apiUrl.Replace("/detect", "/reset");
            try
            {
                var response = await _client.PostAsync(resetUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Server buffer reset successfully.");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to reset server buffer. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Exception during reset: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
