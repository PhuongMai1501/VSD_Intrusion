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
                Timeout = TimeSpan.FromSeconds(2)
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
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

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
                var response = await _client.SendAsync(request);

                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Deserialize JSON thành đối tượng DetectionResponse
                    return JsonConvert.DeserializeObject<MultiPersonDetectionResponse>(responseJson);
                }
                else
                {
                    Console.WriteLine($"❌ Lỗi HTTP: {response.StatusCode}. Phản hồi: {responseJson}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Exception khi gọi API: {ex.Message}");
                return null;
            }
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
