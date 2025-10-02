using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace CameraManager.Messaging
{
    public static class ZaloMessageService
    {
        private const string DefaultCallbackUrl = "https://esms.vn/webhook/";
        private const string DefaultCampaignId = "FireSmokeAlert";
        private const string QueryRecipients = "SELECT STT, Name, SDT FROM alarm_mes WHERE IsActive = 1 AND SDT IS NOT NULL AND TRIM(SDT) <> ''";
        private const string Endpoint = "https://rest.esms.vn/MainService.svc/json/MultiChannelMessage/";

        public static async Task<MessageDispatchResult> SendAsync(
            string message,
            string? area,
            string? severity,
            DateTime? eventTime = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string sanitizedMessage = (message ?? string.Empty).Trim();
                string sanitizedSeverity = string.IsNullOrWhiteSpace(severity) ? sanitizedMessage : severity!.Trim();
                if (string.IsNullOrWhiteSpace(sanitizedMessage))
                {
                    sanitizedMessage = sanitizedSeverity;
                }
                if (string.IsNullOrWhiteSpace(sanitizedSeverity))
                {
                    sanitizedSeverity = "Không xác định";
                }

                string effectiveArea = !string.IsNullOrWhiteSpace(area) ? area!.Trim() : CameraManager.ClassCommon.ProgramName;
                if (string.IsNullOrWhiteSpace(effectiveArea))
                {
                    effectiveArea = "Không xác định";
                }

                string? connStr = CameraManager.ClassSystemConfig.Ins?.m_ClsCommon?.connectionString;
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    const string msg = "Không tìm thấy chuỗi kết nối dữ liệu.";
                    FileLogger.Log("ZaloMessageService: Missing DB connection string");
                    return new MessageDispatchResult { Success = false, Summary = msg };
                }

                var recipients = new List<(int Id, string Name, string Phone)>();
                using (var conn = new MySqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using (var cmd = new MySqlCommand(QueryRecipients, conn))
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            int.TryParse(reader["STT"]?.ToString(), out int id);
                            string name = reader["Name"]?.ToString()?.Trim() ?? string.Empty;
                            string? phone = reader["SDT"]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(phone))
                            {
                                recipients.Add((id, name, phone));
                            }
                        }
                    }
                }

                if (recipients.Count == 0)
                {
                    const string msg = "Không có số điện thoại kích hoạt để gửi Zalo!";
                    FileLogger.Log("ZaloMessageService: No active phone number found");
                    return new MessageDispatchResult { Success = false, Summary = msg };
                }

                var secrets = CameraManager.MessageSecretProvider.GetSecrets();
                if (!secrets.HasZaloCredentials)
                {
                    const string msg = "Chưa cấu hình ApiKey hoặc SecretKey cho Zalo trong file MessageSecrets.ini.";
                    FileLogger.Log("ZaloMessageService: Missing eSMS ApiKey/SecretKey");
                    return new MessageDispatchResult { Success = false, Summary = msg };
                }

                if (!secrets.HasZaloTemplateConfiguration)
                {
                    const string msg = "Chưa cấu hình đủ thông tin OAID, TemplateId hoặc Brandname cho Zalo trong file MessageSecrets.ini.";
                    FileLogger.Log("ZaloMessageService: Missing eSMS OAID/TemplateId/Brandname");
                    //return new ZaloSendResult { Success = false, Summary = msg };
                }

                string callbackUrl = string.IsNullOrWhiteSpace(secrets.EsmsCallbackUrl)
                    ? DefaultCallbackUrl
                    : secrets.EsmsCallbackUrl;
                string campaignId = string.IsNullOrWhiteSpace(secrets.EsmsCampaignId)
                    ? DefaultCampaignId
                    : secrets.EsmsCampaignId;

                string detectionTime = (eventTime ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm:ss");

                var sbResult = new StringBuilder();
                bool anySuccess = false;

                using (var client = new HttpClient())
                {
                    foreach (var recipient in recipients
                        .GroupBy(r => r.Phone)
                        .Select(g => g.First()))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string deptName = string.IsNullOrWhiteSpace(recipient.Name)
                            ? "Không xác định"
                            : recipient.Name;
                        string deptId = recipient.Id > 0 ? recipient.Id.ToString() : "Không xác định";

                        var zaloParams = new[]
                        {
                            effectiveArea,
                            detectionTime,
                            sanitizedSeverity,
                            deptName,
                            deptId
                        };

                        var payload = new
                        {
                            ApiKey = secrets.EsmsApiKey,
                            SecretKey = secrets.EsmsSecretKey,
                            Phone = recipient.Phone,
                            Channels = new[] { "zalo", "sms" },
                            Data = new object[]
                            {
                                new
                                {
                                    TempID = secrets.EsmsTemplateId,
                                    Params = zaloParams,
                                    OAID = secrets.EsmsOaid,
                                    campaignid = campaignId,
                                    CallbackUrl = callbackUrl,
                                    RequestId = Guid.NewGuid().ToString(),
                                    Sandbox = "0",
                                    SendingMode = "1"
                                },
                                new
                                {
                                    Content = sanitizedMessage,
                                    IsUnicode = "0",
                                    SmsType = "2",
                                    Brandname = secrets.EsmsBrandName,
                                    CallbackUrl = callbackUrl,
                                    RequestId = Guid.NewGuid().ToString(),
                                    Sandbox = "0"
                                }
                            }
                        };

                        string jsonPayload = JsonConvert.SerializeObject(payload);
                        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                        try
                        {
                            var response = await client.PostAsync(Endpoint, content, cancellationToken);
                            string result = await response.Content.ReadAsStringAsync(cancellationToken);
                            sbResult.AppendLine($"Phone {recipient.Phone}: {(int)response.StatusCode} - {result}");
                            if (response.IsSuccessStatusCode)
                            {
                                anySuccess = true;
                            }
                        }
                        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            sbResult.AppendLine($"Phone {recipient.Phone}: ERROR - {ex.Message}");
                            FileLogger.LogException(ex, $"ZaloMessageService -> Phone={recipient.Phone}");
                        }
                    }
                }

                string summary = sbResult.ToString().Trim();
                if (summary.Length == 0)
                {
                    summary = "Không có kết quả trả về.";
                }

                return new MessageDispatchResult
                {
                    Success = anySuccess,
                    Summary = summary
                };
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ZaloMessageService.SendAsync");
                return new MessageDispatchResult
                {
                    Success = false,
                    Summary = "Lỗi khi gửi Zalo: " + ex.Message
                };
            }
        }
    }
}
