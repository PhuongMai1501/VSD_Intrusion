using Discord;
using Discord.WebSocket;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CameraManager.Messaging;

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

            int? formatOverride = selectedApp switch
            {
                "Telegram" => 0,
                "Discord" => 1,
                "WhatsApp" => 2,
                "Zalo" => 3,
                _ => null
            };

            if (formatOverride == null)
            {
                MessageBox.Show("Chức năng gửi cho ứng dụng này chưa được hỗ trợ.");
                return;
            }

            if (formatOverride == 1)
            {
                await SendDiscordMessageAsync(message);
                MessageBox.Show("Đã gửi tin nhắn Discord!");
                return;
            }

            if (formatOverride == 2)
            {
                MessageBox.Show("WhatsApp hiện chưa được hỗ trợ gửi tin.");
                return;
            }

            string content = message ?? string.Empty;
            string area = ClassCommon.ProgramName;
            var result = await MessagingDispatcher.SendAsync(content, area, content, formatOverride.Value);
            string caption = result.Success ? "Gửi thành công" : "Gửi thất bại";
            MessageBox.Show(result.Summary, caption);
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
