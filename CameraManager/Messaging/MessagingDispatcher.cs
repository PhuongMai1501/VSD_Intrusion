using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CameraManager.Messaging
{
    public static class MessagingDispatcher
    {
        public static async Task<MessageDispatchResult> SendAsync(
            string message,
            string? area,
            string? severity,
            int format,
            DateTime? eventTime = null,
            CancellationToken cancellationToken = default)
        {
            switch (format)
            {
                case 0:
                    return await SendTelegramAsync(message, cancellationToken);
                case 3:
                    return await ZaloMessageService.SendAsync(message, area, severity, eventTime, cancellationToken);
                default:
                    return new MessageDispatchResult
                    {
                        Success = false,
                        Summary = "Ứng dụng gửi tin này chưa được hỗ trợ."
                    };
            }
        }

        private static async Task<MessageDispatchResult> SendTelegramAsync(string message, CancellationToken cancellationToken)
        {
            string sanitizedMessage = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sanitizedMessage))
            {
                return new MessageDispatchResult
                {
                    Success = false,
                    Summary = "Không có nội dung để gửi Telegram."
                };
            }

            var secrets = MessageSecretProvider.GetSecrets();
            if (!secrets.HasTelegramConfiguration)
            {
                return new MessageDispatchResult
                {
                    Success = false,
                    Summary = "Chưa cấu hình Telegram Bot Token trong MessageSecrets.ini."
                };
            }

            string? connStr = ClassSystemConfig.Ins?.m_ClsCommon?.connectionString;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                return new MessageDispatchResult
                {
                    Success = false,
                    Summary = "Không tìm thấy chuỗi kết nối dữ liệu."
                };
            }

            var recipients = new List<(string Name, string SDT, string ChatID)>();
            using (var conn = new MySql.Data.MySqlClient.MySqlConnection(connStr))
            {
                await conn.OpenAsync(cancellationToken);
                const string sql = "SELECT Name, SDT, ChatID FROM alarm_mes WHERE IsActive = 1 AND ChatID IS NOT NULL AND TRIM(ChatID) <> ''";
                using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var name = reader["Name"]?.ToString()?.Trim() ?? string.Empty;
                        var sdt = reader["SDT"]?.ToString()?.Trim() ?? string.Empty;
                        var raw = reader["ChatID"]?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        var parts = raw
                            .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s));
                        foreach (var id in parts)
                        {
                            recipients.Add((name, sdt, id));
                        }
                    }
                }
            }

            if (recipients.Count == 0)
            {
                return new MessageDispatchResult
                {
                    Success = false,
                    Summary = "Không tìm thấy ChatID Telegram đang kích hoạt."
                };
            }

            recipients = recipients
                .GroupBy(r => (r.ChatID, r.Name, r.SDT))
                .Select(g => g.First())
                .ToList();

            var sb = new StringBuilder();
            bool anySuccess = false;

            using (var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(3.5) })
            {
                foreach (var r in recipients)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (string.IsNullOrWhiteSpace(r.ChatID))
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID=<EMPTY> | SDT={r.SDT} | Status=FAIL (empty)",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            sb.AppendLine($"ChatID <EMPTY>: Bỏ qua");
                            continue;
                        }

                        string url = $"https://api.telegram.org/bot{secrets.TelegramBotToken}/sendMessage?chat_id={r.ChatID}&text={Uri.EscapeDataString(sanitizedMessage)}";
                        var resp = await client.GetAsync(url, cancellationToken);
                        bool ok = resp.IsSuccessStatusCode;

                        ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                            $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status={(ok ? "SUCCESS" : "FAIL (HTTP)")}",
                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

                        sb.AppendLine($"ChatID {r.ChatID}: {(ok ? "SUCCESS" : $"FAIL ({(int)resp.StatusCode})")}");
                        if (ok)
                        {
                            anySuccess = true;
                        }
                    }
                    catch (TaskCanceledException tce)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status=TIMEOUT ({tce.Message})",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            FileLogger.LogException(tce, $"MessagingDispatcher Telegram TIMEOUT -> ChatID={r.ChatID}");
                            sb.AppendLine($"ChatID {r.ChatID}: TIMEOUT");
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (Exception exSend)
                    {
                        ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                            $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status=FAIL (EXCEPTION: {exSend.Message})",
                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                        FileLogger.LogException(exSend, $"MessagingDispatcher Telegram -> ChatID={r.ChatID}");
                        sb.AppendLine($"ChatID {r.ChatID}: ERROR - {exSend.Message}");
                    }
                }
            }

            string summary = sb.ToString().Trim();
            if (summary.Length == 0)
            {
                summary = "Không có kết quả gửi Telegram.";
            }

            return new MessageDispatchResult
            {
                Success = anySuccess,
                Summary = summary
            };
        }
    }
}
