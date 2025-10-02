using Discord;
using Discord.WebSocket;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CameraManager
{
    public partial class FormConfigMessage : Form
    {
        private DiscordSocketClient? _discordClient;

        public FormConfigMessage()
        {
            InitializeComponent();
        }

        private void InitComboBoxes()
        {
            // Populate application selection combo box
            cmbAppSelect.Items.Clear();
            cmbAppSelect.Items.AddRange(new object[]
            {
                "Telegram",
                "Discord",
                "WhatsApp",
                "Zalo"
            });
            cmbAppSelect.SelectedIndex = ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage; // Default to Telegram

            // Populate message content combo box
            cmbMessageSelect.Items.Clear();
            cmbMessageSelect.Items.Add("Fire alarm!");
            cmbMessageSelect.Items.Add("Smoke alarm!"); // Added this line
            cmbMessageSelect.SelectedIndex = 0;
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            string selectedApp = cmbAppSelect.SelectedItem?.ToString();
            string message = cmbMessageSelect.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(selectedApp))
            {
                MessageBox.Show("Vui lòng chọn ứng dụng để lưu cấu hình.");
                return;
            }

            bool supported = true;
            switch (selectedApp)
            {
                case "Telegram":
                    ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage = 0;
                    break;
                case "Discord":
                    ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage = 1;
                    break;
                case "WhatsApp":
                    ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage = 2;
                    break;
                case "Zalo":
                    ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage = 3;
                    break;
                default:
                    MessageBox.Show("Chức năng gửi cho ứng dụng này chưa được hỗ trợ.");
                    supported = false;
                    break;
            }

            if (supported)
            {
                SaveDeviceConfig(true);
            }
        }

        #region Save/Load Config
        public void SaveDeviceConfig(bool ShowMessage)
        {
            string file_name = Directory.GetCurrentDirectory() + @"\Config Setting\SendMessageConfig.ini";

            if (!System.IO.File.Exists(file_name))
            {
                System.IO.Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\Config Setting");
            }

            try
            {
                using (StreamWriter objWriter = new StreamWriter(file_name))
                {
                    // SAVING
                    objWriter.WriteLine("[SEND MESSAGE MODE]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage));
                    // ByPass: 1=bỏ qua (không gửi), 0=gửi
                    objWriter.WriteLine("[BYPASS ALERT]  " + (chkByPass.Checked ? "1" : "0"));
                    objWriter.Close();
                }
                if (ShowMessage)
                {
                    MessageBox.Show("Saved Success");
                }
            }
            catch
            {
                if (ShowMessage)
                {
                    MessageBox.Show("Save Fail");
                }
            }

        }
        private void LoadDeviceConfig(bool ShowMessage)
        {
            string file_name = Directory.GetCurrentDirectory() + @"\Config Setting\SendMessageConfig.ini";

            if (!System.IO.Directory.Exists(Directory.GetCurrentDirectory() + @"\Config Setting"))
            {
                System.IO.Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\Config Setting");
            }
            else
            {
                try
                {
                    try
                    {
                        ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSendMessage = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "SEND MESSAGE MODE", "0"), 0);
                        // đọc BYPASS ALERT; nếu không có thì suy ra từ ENABLE ALERT (enable=1 => bypass=0)
                        string bypassStr = ClassCommon.GetConfig(file_name, "BYPASS ALERT", "");
                        if (string.IsNullOrWhiteSpace(bypassStr))
                        {
                            string enableStr = ClassCommon.GetConfig(file_name, "ENABLE ALERT", "0");
                            int enable = 0; int.TryParse(enableStr, out enable);
                            ClassSystemConfig.Ins.m_ClsCommon.b_ByPassAlarm = (enable == 1) ? 0 : 1;
                        }
                        else
                        {
                            int bypass = 0; int.TryParse(bypassStr, out bypass);
                            ClassSystemConfig.Ins.m_ClsCommon.b_ByPassAlarm = (bypass == 1) ? 1 : 0;
                        }
                        chkByPass.Checked = (ClassSystemConfig.Ins.m_ClsCommon.b_ByPassAlarm == 1);
                    }
                    catch { }

                }
                catch
                {
                    if (ShowMessage)
                    {
                        MessageBox.Show("Load Fail");
                    }
                }
            }

        }

        #endregion

        private void FormConfigMessage_Load(object sender, EventArgs e)
        {
            LoadDeviceConfig(false);
            InitComboBoxes();
            UpdateAlarmMes();
            // Cập nhật màu nền ByPass theo trạng thái và gắn sự kiện thay đổi
            UpdateByPassCheckboxStyle();
        }
        private void UpdateByPassCheckboxStyle()
        {
            if (chkByPass.Checked)
            {
                chkByPass.BackColor = System.Drawing.Color.LimeGreen;
                chkByPass.ForeColor = System.Drawing.Color.White;
            }
            else
            {
                chkByPass.BackColor = System.Drawing.Color.Gainsboro;
                chkByPass.ForeColor = System.Drawing.Color.Black;
            }
        }
        private void chkByPass_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // Cập nhật runtime: ByPass 1=bật (không gửi), 0=tắt (gửi)
                ClassSystemConfig.Ins.m_ClsCommon.b_ByPassAlarm = chkByPass.Checked ? 1 : 0;
                UpdateByPassCheckboxStyle();
                // Không lưu ngay, bấm Save để lưu vào file
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                    $"BYPASS {(chkByPass.Checked ? "ON" : "OFF")}",
                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
            catch { }
        }

        #region Config Message
        private async void btnTest_Click(object sender, EventArgs e)
        {
            string selectedApp = cmbAppSelect.SelectedItem?.ToString();
            string message = cmbMessageSelect.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(selectedApp))
            {
                MessageBox.Show("Vui lòng chọn ứng dụng để kiểm tra gửi tin.");
                return;
            }

            if (selectedApp == "Telegram")
            {
                await SendTelegramMessageAsync(message);
                MessageBox.Show("Đã gửi tin nhắn Telegram!");
            }
            else if (selectedApp == "Discord")
            {
                await SendDiscordMessageAsync(message);
                MessageBox.Show("Đã gửi tin nhắn Discord!");
            }
            else if (selectedApp == "Zalo")
            {
                await SendZaloMessageAsync(message);
            }
            else
            {
                MessageBox.Show("Chức năng gửi cho ứng dụng này chưa được hỗ trợ.");
            }
        }

        private async Task SendZaloMessageAsync(string? message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    MessageBox.Show("Vui lòng nhập nội dung!");
                    return;
                }

                string sanitizedMessage = message.Trim();

                string? connStr = ClassSystemConfig.Ins?.m_ClsCommon?.connectionString;
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    MessageBox.Show("Không tìm thấy chuỗi kết nối dữ liệu.");
                    FileLogger.Log("SendZaloMessageAsync: Missing DB connection string");
                    return;
                }

                var recipients = new List<(int Id, string Name, string Phone)>();
                using (var conn = new MySqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    const string sql = "SELECT STT, Name, SDT FROM alarm_mes WHERE IsActive = 1 AND SDT IS NOT NULL AND TRIM(SDT) <> ''";
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int.TryParse(reader["STT"]?.ToString(), out int id);
                            var name = reader["Name"]?.ToString()?.Trim() ?? string.Empty;
                            var phone = reader["SDT"]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(phone))
                            {
                                recipients.Add((id, name, phone));
                            }
                        }
                    }
                }

                if (recipients.Count == 0)
                {
                    MessageBox.Show("Không có số điện thoại kích hoạt để gửi Zalo!");
                    FileLogger.Log("SendZaloMessageAsync: No active phone number found");
                    return;
                }

                var secrets = MessageSecretProvider.GetSecrets();
                if (string.IsNullOrWhiteSpace(secrets.EsmsApiKey) || string.IsNullOrWhiteSpace(secrets.EsmsSecretKey))
                {
                    MessageBox.Show("Chưa cấu hình ApiKey hoặc SecretKey cho Zalo trong file MessageSecrets.ini.");
                    FileLogger.Log("SendZaloMessageAsync: Missing eSMS ApiKey/SecretKey");
                    return;
                }

                if (string.IsNullOrWhiteSpace(secrets.EsmsOaid) || string.IsNullOrWhiteSpace(secrets.EsmsTemplateId) || string.IsNullOrWhiteSpace(secrets.EsmsBrandName))
                {
                    MessageBox.Show("Chưa cấu hình đủ thông tin OAID, TemplateId hoặc Brandname cho Zalo trong file MessageSecrets.ini.");
                    FileLogger.Log("SendZaloMessageAsync: Missing eSMS OAID/TemplateId/Brandname");
                    return;
                }

                string callbackUrl = string.IsNullOrWhiteSpace(secrets.EsmsCallbackUrl)
                    ? "https://esms.vn/webhook/"
                    : secrets.EsmsCallbackUrl;
                string campaignId = string.IsNullOrWhiteSpace(secrets.EsmsCampaignId)
                    ? "FireSmokeAlert"
                    : secrets.EsmsCampaignId;

                const string url = "https://rest.esms.vn/MainService.svc/json/MultiChannelMessage/";
                var sbResult = new StringBuilder();

                string area = "Phong 1";
                if (string.IsNullOrWhiteSpace(area))
                {
                    area = ClassCommon.ProgramName;
                }

                string severity = sanitizedMessage;
                string detectionTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                using (var client = new HttpClient())
                {
                    foreach (var recipient in recipients
                        .GroupBy(r => r.Phone)
                        .Select(g => g.First()))
                    {
                        string deptName = string.IsNullOrWhiteSpace(recipient.Name)
                            ? "Không xác định"
                            : recipient.Name;
                        string deptId = recipient.Id > 0 ? recipient.Id.ToString() : "Không xác định";

                        var zaloParams = new[]
                        {
                            area,
                            detectionTime,
                            severity,
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
                            var response = await client.PostAsync(url, content);
                            string result = await response.Content.ReadAsStringAsync();
                            sbResult.AppendLine($"Phone {recipient.Phone}: {(int)response.StatusCode} - {result}");
                        }
                        catch (Exception ex)
                        {
                            sbResult.AppendLine($"Phone {recipient.Phone}: ERROR - {ex.Message}");
                            FileLogger.LogException(ex, $"SendZaloMessageAsync -> Phone={recipient.Phone}");
                        }
                    }
                }

                string resultText = sbResult.ToString().Trim();
                if (resultText.Length == 0)
                {
                    resultText = "Không có kết quả trả về.";
                }

                MessageBox.Show(resultText, "Kết quả gửi Zalo");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi gửi Zalo: " + ex.Message, "Lỗi");
                FileLogger.LogException(ex, "SendZaloMessageAsync");
            }
        }

        private async Task SendTelegramMessageAsync(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return;

                var secrets = MessageSecretProvider.GetSecrets();
                if (!secrets.HasTelegramConfiguration)
                {
                    MessageBox.Show("Vui lòng cấu hình Telegram Bot Token trong Config Setting/MessageSecrets.ini.");
                    return;
                }

                string botToken = secrets.TelegramBotToken;
                // Lấy danh sách người nhận: Name, SDT, ChatID (có thể nhiều ID trong 1 ô)
                var recipients = new List<(string Name, string SDT, string ChatID)>();
                string connStr = ClassSystemConfig.Ins?.m_ClsCommon?.connectionString;
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    FileLogger.Log("SendTelegramMessageAsync: Missing DB connection string");
                    return;
                }

                using (var conn = new MySqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    string sql = "SELECT Name, SDT, ChatID FROM alarm_mes WHERE IsActive = 1 AND ChatID IS NOT NULL AND TRIM(ChatID) <> ''";
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var name = reader["Name"]?.ToString()?.Trim() ?? string.Empty;
                            var sdt = reader["SDT"]?.ToString()?.Trim() ?? string.Empty;
                            var raw = reader["ChatID"]?.ToString()?.Trim();
                            if (string.IsNullOrWhiteSpace(raw)) continue;

                            // Hỗ trợ nhiều ChatID trong một ô, phân tách bằng , ; khoảng trắng
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
                    FileLogger.Log("SendTelegramMessageAsync: No ChatID found in alarm_mes");
                    return;
                }

                // Loại bỏ trùng lặp theo ChatID + Name + SDT để giữ tương ứng đúng
                recipients = recipients
                    .GroupBy(r => (r.ChatID, r.Name, r.SDT))
                    .Select(g => g.First())
                    .ToList();

                using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromSeconds(3.5) })
                {
                    foreach (var r in recipients)
                    {
                        try
                        {
                            // Valid sơ bộ chatID (không chặn username @), chỉ để loại bỏ giá trị vô lý
                            if (string.IsNullOrWhiteSpace(r.ChatID))
                            {
                                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                    $"TELEGRAM SEND | Name={r.Name} | ChatID=<EMPTY> | SDT={r.SDT} | Status=FAIL (empty)",
                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                                continue;
                            }

                            string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={r.ChatID}&text={Uri.EscapeDataString(message)}";
                            var resp = await client.GetAsync(url);
                            var ok = resp.IsSuccessStatusCode;

                            // Nếu server trả mã lỗi (ví dụ 400 cho chat_id sai), coi như FAIL nhưng không văng lỗi
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status={(ok ? "SUCCESS" : "FAIL (HTTP)")}",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            FileLogger.Log($"Telegram sent to ChatID={r.ChatID} => {(ok ? "OK" : "HTTP_FAIL")}\n");
                        }
                        catch (TaskCanceledException tce)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status=TIMEOUT ({tce.Message})",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            FileLogger.LogException(tce, $"SendTelegramMessageAsync TIMEOUT -> ChatID={r.ChatID}");
                        }
                        catch (Exception exSend)
                        {
                            // Ghi log thất bại, nhưng không làm crash chương trình
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status=FAIL (EXCEPTION: {exSend.Message})",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            FileLogger.LogException(exSend, $"SendTelegramMessageAsync -> ChatID={r.ChatID}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "SendTelegramMessageAsync");
            }
        }

        private async Task SendDiscordMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Vui lòng nhập nội dung!");
                return;
            }

            var secrets = MessageSecretProvider.GetSecrets();
            if (!secrets.HasDiscordConfiguration || secrets.DiscordChannelId == null)
            {
                MessageBox.Show("Vui lòng cấu hình Discord Bot Token và ChannelId trong Config Setting/MessageSecrets.ini.");
                return;
            }

            string discordBotToken = secrets.DiscordBotToken;
            ulong discordChannelId = secrets.DiscordChannelId.Value;

            // Ensure the Discord client is initialized and connected
            if (_discordClient == null)
            {
                _discordClient = new DiscordSocketClient();
                _discordClient.Log += (msg) => { Console.WriteLine(msg); return Task.CompletedTask; };
                await _discordClient.LoginAsync(TokenType.Bot, discordBotToken);
                await _discordClient.StartAsync();

                // Wait for the client to be ready
                var tcs = new TaskCompletionSource<bool>();
                _discordClient.Ready += () =>
                {
                    tcs.SetResult(true);
                    return Task.CompletedTask;
                };
                await tcs.Task;
            }

            var channel = _discordClient.GetChannel(discordChannelId) as IMessageChannel;
            if (channel != null)
            {
                await channel.SendMessageAsync(message);
                Console.WriteLine("Đã gửi tin nhắn!");
            }
            else
            {
                Console.WriteLine("Không tìm thấy channel!");
            }
        }
        #endregion

        #region DataGridView Events
        public void UpdateAlarmMes()
        {
            using (MySqlConnection connectionMes = new MySqlConnection(ClassSystemConfig.Ins.m_ClsCommon.connectionString))
            {
                try
                {
                    connectionMes.Open();
                    string query = "SELECT STT, Name, SDT, IsActive, ChatID FROM alarm_mes ORDER BY STT DESC";
                    MySqlDataAdapter dataAdapter = new MySqlDataAdapter(query, connectionMes);
                    DataTable dataTable = new DataTable();
                    dataAdapter.Fill(dataTable);
                    dgMessage_Alarm.DataSource = dataTable;

                    // Format DataGridView nếu bạn có hàm riêng
                    ClassSystemConfig.Ins.m_ClsFunc.FormatDataGridView(dgMessage_Alarm);

                    // Thiết lập tiêu đề và căn chỉnh cột
                    dgMessage_Alarm.Columns["STT"].HeaderText = "STT";
                    dgMessage_Alarm.Columns["STT"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgMessage_Alarm.Columns["STT"].Width = 30;

                    dgMessage_Alarm.Columns["Name"].HeaderText = "Name";
                    dgMessage_Alarm.Columns["Name"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgMessage_Alarm.Columns["Name"].Width = 100;

                    dgMessage_Alarm.Columns["SDT"].HeaderText = "Phone number";
                    dgMessage_Alarm.Columns["SDT"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    dgMessage_Alarm.Columns["SDT"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                    dgMessage_Alarm.Columns["ChatID"].HeaderText = "ChatID";
                    dgMessage_Alarm.Columns["ChatID"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgMessage_Alarm.Columns["ChatID"].Width = 150;

                    dgMessage_Alarm.Columns["IsActive"].Visible = false;

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi kết nối: " + ex.Message);
                }
                finally
                {
                    connectionMes.Close();
                }
            }
        }

        public void DeleteAlarmMes(int id)
        {
            using (MySqlConnection connectionMes = new MySqlConnection(ClassSystemConfig.Ins.m_ClsCommon.connectionString))
            {
                try
                {
                    connectionMes.Open();
                    string query = "DELETE FROM alarm_mes WHERE STT = @STT";

                    using (MySqlCommand cmd = new MySqlCommand(query, connectionMes))
                    {
                        cmd.Parameters.AddWithValue("@STT", id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Xóa dữ liệu thành công!");
                            UpdateAlarmMes(); // Refresh DataGridView sau khi xóa
                        }
                        else
                        {
                            MessageBox.Show("Không tìm thấy dữ liệu để xóa.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xóa dữ liệu: " + ex.Message);
                }
            }
        }

        private void dgMessage_Alarm_SelectionChanged(object sender, EventArgs e)
        {
            if (dgMessage_Alarm.CurrentRow == null)
                return;
            int iSelected = dgMessage_Alarm.CurrentRow.Index;

            if (iSelected >= 0 && iSelected < dgMessage_Alarm.Rows.Count)
            {
                txbName.Text = dgMessage_Alarm.Rows[iSelected].Cells[1].Value.ToString();
                txbPhone_num.Text = dgMessage_Alarm.Rows[iSelected].Cells[2].Value.ToString();
                txbChatID.Text = dgMessage_Alarm.Rows[iSelected].Cells["ChatID"].Value?.ToString() ?? string.Empty;
                // Lấy trạng thái IsActive từ DataGridView
                var cellValue = dgMessage_Alarm.Rows[iSelected].Cells["IsActive"].Value;
                if (cellValue != null && cellValue != DBNull.Value)
                {
                    chkEnableAlert.Checked = Convert.ToInt32(cellValue) == 1;
                }
                else
                {
                    chkEnableAlert.Checked = false;
                }
            }

        }

        public void UpdateCameraData(string name, string phone_number, int stt, bool isActive, string chatId = null)
        {
            MySqlConnection connection = new MySqlConnection(ClassSystemConfig.Ins.m_ClsCommon.connectionString);

            try
            {
                connection.Open();
                string query = "UPDATE alarm_mes SET Name = @Name, SDT = @SDT, IsActive = @IsActive, ChatID = @ChatID WHERE STT = @STT";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    // Thêm tham số vào câu lệnh
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@SDT", phone_number);
                    command.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                    command.Parameters.AddWithValue("@ChatID", string.IsNullOrEmpty(chatId) ? (object)DBNull.Value : chatId);
                    command.Parameters.AddWithValue("@STT", stt);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        UpdateAlarmMes();
                        MessageBox.Show("Dữ liệu đã được cập nhật thành công!");
                    }
                    else
                    {
                        MessageBox.Show("Không có dữ liệu được cập nhật.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }

        public void InsertAlarmMes(string name, string phoneNumber, bool isActive, string chatId = null)
        {
            using (MySqlConnection connectionMes = new MySqlConnection(ClassSystemConfig.Ins.m_ClsCommon.connectionString))
            {
                try
                {
                    connectionMes.Open();
                    string query = "INSERT INTO alarm_mes (Name, SDT, IsActive, ChatID) VALUES (@Name, @SDT, @IsActive, @ChatID)";


                    using (MySqlCommand cmd = new MySqlCommand(query, connectionMes))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@SDT", phoneNumber);
                        cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                        cmd.Parameters.AddWithValue("@ChatID", string.IsNullOrEmpty(chatId) ? (object)DBNull.Value : chatId);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Thêm mới dữ liệu thành công!");
                            UpdateAlarmMes(); // Refresh DataGridView sau khi thêm
                        }
                        else
                        {
                            MessageBox.Show("Không có dữ liệu nào được thêm.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi thêm dữ liệu: " + ex.Message);
                }
            }
        }

        private void btnUpdate_Mes_Click(object sender, EventArgs e)
        {
            string name = txbName.Text.Trim();
            string phone_number = txbPhone_num.Text.Trim();
            bool IsActive = chkEnableAlert.Checked;
            string chatId = txbChatID.Text.Trim(); // Hoặc lấy từ một TextBox nếu cần
            UpdateCameraData(name, phone_number, Convert.ToInt32(dgMessage_Alarm.CurrentRow.Cells[0].Value.ToString()), IsActive, chatId);
        }

        private void btnAdd_Mes_Click(object sender, EventArgs e)
        {
            string name = txbName.Text.Trim();
            string phone_number = txbPhone_num.Text.Trim();
            bool IsActive = chkEnableAlert.Checked;
            string chatId = txbChatID.Text.Trim(); // Hoặc lấy từ một TextBox nếu cần

            InsertAlarmMes(name, phone_number, IsActive, chatId);
        }

        private void btnDelete_Mes_Click(object sender, EventArgs e)
        {
            if (dgMessage_Alarm.CurrentRow == null)
            {
                MessageBox.Show("Vui lòng chọn dòng cần xóa!");
                return;
            }

            int id = Convert.ToInt32(dgMessage_Alarm.CurrentRow.Cells["STT"].Value);

            DialogResult result = MessageBox.Show(
                "Bạn có chắc chắn muốn xóa dòng này?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                DeleteAlarmMes(id);
            }
        }

        #endregion
    }
}
