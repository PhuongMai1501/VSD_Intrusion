using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using static Mysqlx.Crud.Order.Types;
using MySqlX.XDevAPI;
using System.Net.Http;
using Mysqlx.Notice;
using System.Threading;
using Org.BouncyCastle.Utilities.Collections;
using System.Data.SqlClient;

namespace CameraManager
{
    public partial class MeasureRecipe2 : UserControl
    {
        #region Define Variables
        private int m_IndexRecipe = 0;
        private int m_indexCamera = 0;

        public Results Result = new Results();
        //CogDisplay Cogdisplay = new CogDisplay();
        //CogImage24PlanarColor InputImage = null;

        MySqlConnection connection;
        HttpClient client = new HttpClient();

        private Form1 main;

        #region Region Draw (PictureBox1)
        private bool _isDrawingRegion = false;
        private readonly List<PointF> _regionPointsNorm = new List<PointF>(4); // normalized [0..1]
        #endregion

        #endregion

        public MeasureRecipe2()
        {
            InitializeComponent();
            // Wire region drawing events
            try
            {
                btnDraw4Point.Click += btnDraw4Point_Click;
                btnClear.Click += btnClear_Click;
                pictureBox1.MouseClick += pictureBox1_MouseClick;
                pictureBox1.Paint += pictureBox1_Paint;
            }
            catch { }
        }
        public void InitPageSetup(Form1 obj, int index, string title = "")
        {
            lbTitleName.Text = (title.Trim() != "") ? title : "SETTING";
            main = obj;
            m_IndexRecipe = index;
            RefreshSetupUI();

        }
        #region database
        public void UpdateDataBase()
        {
            try
            {
                // Mở kết nối
                connection = new MySqlConnection(ClassSystemConfig.Ins.m_ClsCommon.connectionString);
                connection.Open();
                // Viết câu lệnh SQL để truy vấn dữ liệu
                string query = "SELECT `STT`, `Name` FROM `camera_list`";
                MySqlDataAdapter dataAdapter = new MySqlDataAdapter(query, connection);

                // Tạo DataTable để lưu trữ dữ liệu
                DataTable dataTable = new DataTable();

                // Điền dữ liệu vào DataTable
                dataAdapter.Fill(dataTable);

                // Gán DataTable vào DataGridView
                dgviewCamera.DataSource = dataTable;

                ClassSystemConfig.Ins.m_ClsFunc.FormatDataGridView(dgviewCamera);
                dgviewCamera.Columns["STT"].HeaderText = "STT";
                dgviewCamera.Columns["STT"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgviewCamera.Columns["STT"].Width = 80;
                dgviewCamera.Columns["Name"].HeaderText = "Camera";
                dgviewCamera.Columns["Name"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgviewCamera.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối: " + ex.Message);
            }
            finally
            {
                //Đóng kết nối
                connection.Close();
            }
        }

        #endregion

        #region Function
        public void RefreshSetupUI()
        {
            btnRefreshRecipe_Click(null, null);
        }
        private void UncheckControl(CheckBox control)
        {
            //for (int i = 0; i < ListCheckControl.Length; i++)
            //{
            //    ListCheckControl[i].Checked = (control == ListCheckControl[i]);
            //}
        }
        public void SetOutputView()
        {
            try
            {
                //panelMain.Controls.Clear();
                //panelMain.Controls.Add(Cogdisplay);
                //Cogdisplay.Dock = DockStyle.Fill;
                //Cogdisplay.BackColor = Color.DimGray;
                //if (Cogdisplay.Image != null)
                //    Cogdisplay.Fit(true);
            }
            catch { }
        }
        public void ShowOnScreen()
        {
            this.BringToFront();
        }
        #endregion

        #region Run and Simulate
        List<string> ListImagePaths = new List<string>();
        int m_indexRun = 0;
        private async void toolStripRun_Click(object sender, EventArgs e)
        {
            try
            {
                if (ListImagePaths.Count == 0)
                {
                    ClassCommon.ShowMessageBoxShort("None InputImage", "Null", 500);
                    return;
                }

                string path = ListImagePaths[0];
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Load original image
                Bitmap original;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var imgTmp = Image.FromStream(fs))
                {
                    original = new Bitmap(imgTmp);
                }

                int detectSize = Math.Max(1, ClassSystemConfig.Ins?.m_ClsCommon?.DetectionInputSize ?? 640);
                using (var resized = ResizeToSquare(original, detectSize))
                {
                    // Encode JPEG
                    byte[] jpeg = EncodeJpeg(resized, 75L);
                    string base64 = Convert.ToBase64String(jpeg);

                    // Send to API
                    string apiUrl = ClassSystemConfig.Ins?.m_ClsCommon?.url_Server ?? "http://127.0.0.1:8000/predict";
                    var payload = new { image = base64 };
                    var json = JsonConvert.SerializeObject(payload);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var resp = await client.PostAsync(apiUrl, content);
                    if (!resp.IsSuccessStatusCode)
                    {
                        toolStripStatus.Text = $"API error: {(int)resp.StatusCode}";
                        original.Dispose();
                        return;
                    }
                    var respText = await resp.Content.ReadAsStringAsync();
                    var detectionsRaw = JsonConvert.DeserializeObject<List<Detection>>(respText) ?? new List<Detection>();

                    // Normalize to original frame [0..1]
                    var detections = NormalizeDetectionsToOriginalFrame(detectionsRaw, original.Width, original.Height, detectSize);

                    // Thresholds
                    double sParamFlame = Convert.ToDouble(num_x1.Value);
                    double sParamSmoke = Convert.ToDouble(num_y1.Value);

                    // Draw filtered detections
                    using (var annotated = (Bitmap)original.Clone())
                    using (var g = Graphics.FromImage(annotated))
                    {
                        foreach (var d in detections)
                        {
                            if (d == null) continue;
                            var label = (d.label ?? string.Empty).Trim();
                            bool isFire = label.Equals("fire", StringComparison.OrdinalIgnoreCase) || label.Equals("flame", StringComparison.OrdinalIgnoreCase) || label.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 || label.IndexOf("flame", StringComparison.OrdinalIgnoreCase) >= 0;
                            bool isSmoke = label.Equals("smoke", StringComparison.OrdinalIgnoreCase) || label.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (isFire && d.score < sParamFlame) continue;
                            if (isSmoke && d.score < sParamSmoke) continue;
                            if (!isFire && !isSmoke) continue;

                            // map to pixels
                            int x1 = (int)(d.x1 * annotated.Width);
                            int y1 = (int)(d.y1 * annotated.Height);
                            int x2 = (int)(d.x2 * annotated.Width);
                            int y2 = (int)(d.y2 * annotated.Height);

                            int left = Math.Max(0, Math.Min(annotated.Width - 1, Math.Min(x1, x2)));
                            int top = Math.Max(0, Math.Min(annotated.Height - 1, Math.Min(y1, y2)));
                            int right = Math.Max(0, Math.Min(annotated.Width - 1, Math.Max(x1, x2)));
                            int bottom = Math.Max(0, Math.Min(annotated.Height - 1, Math.Max(y1, y2)));
                            int w = Math.Max(1, right - left);
                            int h = Math.Max(1, bottom - top);

                            using (var pen = new Pen(Color.Red, 2))
                            {
                                g.DrawRectangle(pen, new Rectangle(left, top, w, h));
                            }
                            using (var font = new Font("Arial", 11, FontStyle.Bold))
                            using (var brush = new SolidBrush(Color.White))
                            {
                                string txt = $"{(isFire ? "FIRE" : "SMOKE")} {d.score:F2}";
                                g.DrawString(txt, font, brush, new PointF(left + 3, Math.Max(0, top - 18)));
                            }
                        }

                        // Show annotated image
                        var old = pictureBox1.Image;
                        pictureBox1.Image = (Bitmap)annotated.Clone();
                        pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                        old?.Dispose();
                    }
                }

                sw.Stop();
                toolStripProcessTime.Text = $"Time: {sw.ElapsedMilliseconds} ms";
                toolStripStatus.Text = "Done";

                original.Dispose();
            }
            catch (Exception ex)
            {
                toolStripStatus.Text = "Error";
                try { ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION, $"RunImage -> {ex.Message}", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true); } catch { }
                MessageBox.Show($"Lỗi xử lý: {ex.Message}");
            }
        }

        private class Detection
        {
            public string label { get; set; }
            public double x1 { get; set; }
            public double y1 { get; set; }
            public double x2 { get; set; }
            public double y2 { get; set; }
            public double score { get; set; }
        }

        private static Bitmap ResizeToSquare(Bitmap src, int size)
        {
            if (src == null) return null;
            if (size <= 0) return (Bitmap)src.Clone();
            double scale = Math.Min((double)size / src.Width, (double)size / src.Height);
            int newW = Math.Max(1, (int)Math.Round(src.Width * scale));
            int newH = Math.Max(1, (int)Math.Round(src.Height * scale));
            int offsetX = (size - newW) / 2;
            int offsetY = (size - newH) / 2;
            var dst = new Bitmap(size, size);
            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(Color.Black);
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, new Rectangle(offsetX, offsetY, newW, newH));
            }
            return dst;
        }

        private static byte[] EncodeJpeg(Bitmap bmp, long quality)
        {
            using var ms = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.MimeType == "image/jpeg");
            if (encoder == null)
            {
                bmp.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            bmp.Save(ms, encoder, encParams);
            return ms.ToArray();
        }

        private List<Detection> NormalizeDetectionsToOriginalFrame(List<Detection> input, int frameW, int frameH, int squareSize)
        {
            try
            {
                if (input == null || input.Count == 0) return new List<Detection>();
                if (frameW <= 0 || frameH <= 0 || squareSize <= 0) return new List<Detection>(input);

                double scale = Math.Min((double)squareSize / frameW, (double)squareSize / frameH);
                double newW = frameW * scale;
                double newH = frameH * scale;
                double offX = (squareSize - newW) / 2.0;
                double offY = (squareSize - newH) / 2.0;

                var list = new List<Detection>(input.Count);
                foreach (var d in input)
                {
                    if (d == null) continue;
                    bool squareNormalized = d.x2 <= 1.5 && d.y2 <= 1.5 && d.x1 >= 0 && d.y1 >= 0;

                    double sx1 = squareNormalized ? d.x1 * squareSize : d.x1;
                    double sy1 = squareNormalized ? d.y1 * squareSize : d.y1;
                    double sx2 = squareNormalized ? d.x2 * squareSize : d.x2;
                    double sy2 = squareNormalized ? d.y2 * squareSize : d.y2;

                    double ox1 = (sx1 - offX) / scale;
                    double oy1 = (sy1 - offY) / scale;
                    double ox2 = (sx2 - offX) / scale;
                    double oy2 = (sy2 - offY) / scale;

                    ox1 = Math.Max(0, Math.Min(frameW, ox1));
                    oy1 = Math.Max(0, Math.Min(frameH, oy1));
                    ox2 = Math.Max(0, Math.Min(frameW, ox2));
                    oy2 = Math.Max(0, Math.Min(frameH, oy2));

                    list.Add(new Detection
                    {
                        label = d.label,
                        score = d.score,
                        x1 = frameW > 0 ? ox1 / frameW : 0,
                        y1 = frameH > 0 ? oy1 / frameH : 0,
                        x2 = frameW > 0 ? ox2 / frameW : 0,
                        y2 = frameH > 0 ? oy2 / frameH : 0,
                    });
                }
                return list;
            }
            catch
            {
                return new List<Detection>(input);
            }
        }

        private void toolStripRunNext_Click(object sender, EventArgs e)
        {
            if (ListImagePaths.Count == 0)
            {
                ClassCommon.ShowMessageBoxShort("None InputImage", "Null", 500);
            }
            else
            {
                string path = ListImagePaths[0];
                if (ListImagePaths.Count > 1)
                {
                    if (m_indexRun >= 0 && m_indexRun < ListImagePaths.Count)
                    {
                        path = ListImagePaths[m_indexRun];
                        m_indexRun++;
                    }
                    else
                    {
                        m_indexRun = 0;
                    }

                }
                else
                {
                    m_indexRun = 0;
                }
                //CogImage8Grey image = new CogImage8Grey((Bitmap)Image.FromFile(path));
                //SetImage(Result.OutputImage,Result.Graphics,true);
            }
        }
        private void toolStripOpenImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Images |*.bmp;*.png;*.jpg;*.jpeg;";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                ListImagePaths.Clear();
                ListImagePaths.Add(fileDialog.FileName);
                // Load ngay ảnh đầu tiên vào pictureBox1 để hiển thị
                try
                {
                    DisplayImageToPictureBox(fileDialog.FileName);
                }
                catch (Exception ex)
                {
                    try { ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION, $"OpenImage -> {ex.Message}", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true); } catch { }
                    MessageBox.Show($"Không thể mở ảnh: {ex.Message}", "Lỗi mở ảnh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void toolStripopenFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fldDialog = new FolderBrowserDialog();
            if (fldDialog.ShowDialog() == DialogResult.OK)
            {
                ////String[] listImages = FormVisionSetting.GetFilesFrom(fldDialog.SelectedPath, false);
                //if (listImages != null)
                //{
                //    ListImagePaths.Clear();
                //    ListImagePaths.AddRange(listImages);
                //}
            }
        }

        private void DisplayImageToPictureBox(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                throw new FileNotFoundException("File không tồn tại", path);
            }

            // Đọc ảnh an toàn để không giữ file lock
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            using (var imgTmp = System.Drawing.Image.FromStream(fs))
            {
                var bmp = new Bitmap(imgTmp);
                var old = pictureBox1.Image;
                pictureBox1.Image = bmp;
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                old?.Dispose();
            }
        }

        #endregion

        #region DataTable:
        private void btnSortDown1_Click(object sender, EventArgs e)
        {
            if (dgviewCamera.CurrentRow == null)
                return;
            int iSelected = dgviewCamera.CurrentRow.Index;
            //if (iSelected >= 0 && iSelected < ListBaseTool.Count - 1)
            //{
            //    ICogTool tool_select = ListBaseTool[iSelected];
            //    DataGridViewRow row_select = dataGridView1.Rows[iSelected];
            //    dataGridView1.Rows.Remove(row_select);
            //    dataGridView1.Rows.Insert(iSelected + 1, row_select);

            //    ListBaseTool.Remove(tool_select);
            //    ListBaseTool.Insert(iSelected + 1, tool_select);


            //    dataGridView1.Rows[iSelected].Selected = false;

            //    for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
            //    {
            //        dataGridView1.Rows[iRow].Cells[0].Value = (iRow + 1);
            //    }
            //}
        }
        private void btnSortUp1_Click(object sender, EventArgs e)
        {
            if (dgviewCamera.CurrentRow == null)
                return;
            int iSelected = dgviewCamera.CurrentRow.Index;
            //if (iSelected > 0 && iSelected < ListBaseTool.Count)
            //{
            //    ICogTool tool_select = ListBaseTool[iSelected];
            //    DataGridViewRow row_select = dataGridView1.Rows[iSelected];
            //    dataGridView1.Rows.Remove(row_select);
            //    dataGridView1.Rows.Insert(iSelected - 1, row_select);

            //    ListBaseTool.Remove(tool_select);
            //    ListBaseTool.Insert(iSelected - 1, tool_select);

            //    dataGridView1.Rows[iSelected].Selected = false;

            //    for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
            //    {
            //        dataGridView1.Rows[iRow].Cells[0].Value = (iRow + 1);
            //    }

            //}
        }
        private void btnDeleteAllToolBase_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you want to delete all base tools", "Delete All", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                dgviewCamera.Rows.Clear();

                ClassCommon.ShowMessageBoxShort("Deleted All Tool Complete", "Delete All", 1000);
            }
        }
        #endregion

        #region Measure Run Tool

        //public void SetImage(ICogImage image)
        //{
        //    if (Cogdisplay.InvokeRequired)
        //    {
        //        Cogdisplay.Invoke(new MethodInvoker(delegate
        //        {
        //            SetImage(image);

        //        }));
        //    }
        //    else
        //    {
        //        Cogdisplay.Image = image;
        //        Cogdisplay.AutoFit = true;
        //        //if (clear_graphic)
        //        //{
        //        //    try
        //        //    {
        //        //        cogRecordDisplay.StaticGraphics.Clear();
        //        //    }
        //        //    catch { }
        //        //}
        //        //foreach (ICogGraphic g in graphic)
        //        //{
        //        //    cogRecordDisplay.StaticGraphics.Add(g, "");
        //        //}
        //    }
        //}
        //public void ShowGraphicResult(ICogGraphic graphic)
        //{
        //    if (graphic != null)
        //    {
        //        if (Result.Graphics == null)
        //            Result.Graphics = new CogGraphicCollection();
        //        Result.Graphics.Add(graphic);
        //        Cogdisplay.StaticGraphics.Add(graphic, "graphic");
        //    }
        //}
        //private CogGraphicLabel CreateLabel(double x, double y, double rotation, float fontsize, bool isBold, string text, CogColorConstants color, string spacename)
        //{
        //    CogGraphicLabel label = new CogGraphicLabel();
        //    label.SetXYText(x, y, text);
        //    label.Alignment = CogGraphicLabelAlignmentConstants.TopLeft;
        //    label.Font = new Font("Century Gothic", fontsize, isBold ? FontStyle.Bold : FontStyle.Regular);
        //    label.Color = color;
        //    label.Rotation = rotation;
        //    if (spacename.Trim().Length > 0)
        //    {
        //        label.SelectedSpaceName = spacename;
        //    }

        //    return label;
        //}
        //CogPointMarker point_edit = null;
        //private void cbEditGraphic_CheckedChanged(object sender, EventArgs e)
        //{

        //}
        //private void btnShowGrid_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (Cogdisplay.Image != null)
        //    {
        //        if (btnShowGrid.Checked)
        //        {
        //            int grid_size = 0;
        //            int.TryParse(txbGridSize.Text, out grid_size);
        //            if (grid_size < 2)
        //                grid_size = 2;
        //            else
        //                if (grid_size > 40)
        //                grid_size = 40;
        //            ShowGridDisplay(Cogdisplay.Image, grid_size, CogColorConstants.Blue);
        //        }
        //        else
        //        {
        //            ClearGridDisplay();
        //        }
        //    }
        //    else
        //    {
        //        btnShowGrid.Checked = false;
        //    }
        //    btnShowGrid.BackColor = btnShowGrid.Checked ? Color.BlueViolet : Color.Transparent;
        //    txbGridSize.Enabled = !btnShowGrid.Checked;
        //}
        //private void ShowGridDisplay(ICogImage image, int Number = 5, CogColorConstants color = CogColorConstants.Yellow)
        //{
        //    double W = image.Width;
        //    double H = image.Height;
        //    if (Number < 1)
        //        Number = 2;
        //    CogLineSegment[] line_horz = new CogLineSegment[Number];
        //    CogLineSegment[] line_vert = new CogLineSegment[Number];

        //    try
        //    {
        //        for (int i = 0; i < Number; i++)
        //        {
        //            line_horz[i] = new CogLineSegment();
        //            line_vert[i] = new CogLineSegment();

        //            // Create Line
        //            double dx = image.Width / Number;
        //            double dy = image.Height / Number;
        //            line_vert[i].SetStartLengthRotation(dx * i, 0, H, Math.PI / 2);
        //            line_horz[i].SetStartLengthRotation(0, dy * i, W, 0);

        //            line_vert[i].Color = color;
        //            line_horz[i].Color = color;

        //            line_vert[i].LineStyle = (i % 2 == 0) ? CogGraphicLineStyleConstants.Solid : CogGraphicLineStyleConstants.Dot;
        //            line_horz[i].LineStyle = (i % 2 == 0) ? CogGraphicLineStyleConstants.Solid : CogGraphicLineStyleConstants.Dot;

        //            line_vert[i].SelectedSpaceName = "#";
        //            line_horz[i].SelectedSpaceName = "#";

        //            Cogdisplay.InteractiveGraphics.Add(line_vert[i], "grid", false);
        //            Cogdisplay.InteractiveGraphics.Add(line_horz[i], "grid", false);
        //        }
        //    }
        //    catch { }
        //}
        //public void ClearGridDisplay()
        //{
        //    try
        //    {
        //        Cogdisplay.InteractiveGraphics.Remove("grid");

        //    }
        //    catch { }
        //}
        #endregion

        public class Results
        {
            //public ICogImage OutputImage = null;
            //public CogGraphicCollection Graphics = new CogGraphicCollection();
            public List<double> ListData = new List<double>();
            public List<string> ListHeader = new List<string>();
            public bool FinalResult = false;
            public string Status = "";
            public double ProcessingTime = 0;
            public void Clear()
            {
                //OutputImage = null;
                //Graphics = new CogGraphicCollection();
                Status = "";
                FinalResult = false;
                ProcessingTime = 0;
                ListData = new List<double>();
                ListHeader = new List<string>();
            }
        }

        public class RecipeConfig
        {
            public int Index { get; set; }
            public bool IsReady { get; set; }
            public bool IsUse2PM = false;
            public bool IsCropImage = false;
            public bool ShowCommonGraphic = true;
            public bool ShowResultGraphic = true;
            public bool ShowTextGraphic = true;
            public double MaxRefTolerance = 0.03;
            public List<ITool> ListToolMeasure = new List<ITool>();
        }
        public class ITool
        {
            public string Toolname = "";
            public int Mode = 0;
            public bool ShowGraphic = false;
            public List<string[]> Inputs = new List<string[]>();
            public double[] Spec = new double[3] { 0, 0, 0 };
            public double OffsetRatio = 0;
            public PointF TextLocation = new PointF(0, 0);
            public bool IntersecLine = true;
            public double RefInputValue = 0;
            public bool RefInputEnable = false;
        }
        public void UpdateParam(string updateQuery, MySqlConnection connection, int interval, double flame, double smoke, int stt)
        {
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();  // 🔥 Mở kết nối trước khi dùng
                }
                // Mở kết nối
                using (MySqlCommand cmd = new MySqlCommand(updateQuery, connection))
                {
                    // Thêm các tham số vào câu lệnh SQL
                    cmd.Parameters.AddWithValue("@Interval", interval);
                    cmd.Parameters.AddWithValue("@Flame", flame);
                    cmd.Parameters.AddWithValue("@Smoke", smoke);
                    cmd.Parameters.AddWithValue("@STT", stt);  // hoặc cmd.Parameters.AddWithValue("@Name", name);

                    // Thực thi câu lệnh UPDATE
                    int rowsAffected = cmd.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
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
                // Xử lý lỗi
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            int stt = m_indexCamera + 1;
            try
            {
                if (_regionPointsNorm.Count == 4)
                {
                    SaveRegionToDatabase(stt, _regionPointsNorm);
                    MessageBox.Show("Đã lưu region (4 điểm) vào database.");
                }
                else
                {
                    var typed = GetRegionFromNumericControls();
                    if (typed != null)
                    {
                        SaveRegionToDatabase(stt, typed);
                        MessageBox.Show("Đã lưu region từ ô nhập liệu vào database.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu region: {ex.Message}");
            }
        }

        private void MeasureRecipe2_Load(object sender, EventArgs e)
        {
            try { pictureBox1.SizeMode = PictureBoxSizeMode.Zoom; } catch { }
        }

        private void btnConfigScreen_Click(object sender, EventArgs e)
        {
            main.Col = Convert.ToInt32(numColCam.Value);
            main.Row = Convert.ToInt32(numRowCam.Value);
            main.LayoutCameraSpreadView();
        }

        private void btnAddToolBase_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_CameraList.Show();
            ClassSystemConfig.Ins.m_CameraList.ShowOnScreen();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Camera List Add",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void btnRefreshRecipe_Click(object sender, EventArgs e)
        {
            UpdateDataBase();
        }

        private void dgviewCamera_SelectionChanged(object sender, EventArgs e)
        {
            if (dgviewCamera.CurrentRow == null)
                return;
            int iSelected = dgviewCamera.CurrentRow.Index;
            m_indexCamera = iSelected;
            int STT = iSelected + 1;

            // Hiển thị region của camera được chọn
            try
            {
                LoadRegionFromDbAndShow(STT);
            }
            catch (Exception ex)
            {
                try { toolStripStatus.Text = $"Load region error: {ex.Message}"; } catch { }
            }
        }

        private void LoadRegionFromDbAndShow(int stt)
        {
            using (var conn = new MySqlConnection(ClassSystemConfig.Ins?.m_ClsCommon?.connectionString))
            {
                conn.Open();
                string sql = "SELECT x1,y1,x2,y2,x3,y3,x4,y4 FROM camera_list WHERE STT=@STT LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@STT", stt);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            double[] vals = new double[8];
                            string[] cols = new[] { "x1","y1","x2","y2","x3","y3","x4","y4" };
                            for (int i = 0; i < 8; i++)
                            {
                                int ord = reader.GetOrdinal(cols[i]);
                                vals[i] = reader.IsDBNull(ord) ? 0.0 : Convert.ToDouble(reader.GetValue(ord));
                            }

                            // Cập nhật controls (px)
                            try
                            {
                                num_x1.Value = (decimal)Math.Round(vals[0], 2);
                                num_y1.Value = (decimal)Math.Round(vals[1], 2);
                                num_x2.Value = (decimal)Math.Round(vals[2], 2);
                                num_y2.Value = (decimal)Math.Round(vals[3], 2);
                                num_x3.Value = (decimal)Math.Round(vals[4], 2);
                                numericUpDown3.Value = (decimal)Math.Round(vals[5], 2);
                                numericUpDown6.Value = (decimal)Math.Round(vals[6], 2);
                                numericUpDown5.Value = (decimal)Math.Round(vals[7], 2);
                            }
                            catch { }

                            // Lưu vào overlay list (px)
                            _regionPointsNorm.Clear();
                            _regionPointsNorm.Add(new PointF((float)vals[0], (float)vals[1]));
                            _regionPointsNorm.Add(new PointF((float)vals[2], (float)vals[3]));
                            _regionPointsNorm.Add(new PointF((float)vals[4], (float)vals[5]));
                            _regionPointsNorm.Add(new PointF((float)vals[6], (float)vals[7]));

                            pictureBox1?.Invalidate();
                        }
                    }
                }
            }
        }

        // ==================== Region Drawing Handlers ====================
        private void btnDraw4Point_Click(object sender, EventArgs e)
        {
            try
            {
                if (pictureBox1?.Image == null)
                {
                    MessageBox.Show("Hãy mở ảnh vào pictureBox1 trước khi vẽ.");
                    return;
                }
                _isDrawingRegion = true;
                _regionPointsNorm.Clear();
                pictureBox1.Cursor = Cursors.Cross;
                pictureBox1.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi bật chế độ vẽ: {ex.Message}");
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                _isDrawingRegion = false;
                _regionPointsNorm.Clear();
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
            }
            catch { }
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (!_isDrawingRegion || pictureBox1?.Image == null) return;
                var p = MapMouseToImageNormalized(pictureBox1, e.Location);
                if (p.HasValue)
                {
                    if (_regionPointsNorm.Count < 4)
                    {
                        _regionPointsNorm.Add(p.Value);
                        pictureBox1.Invalidate();
                        if (_regionPointsNorm.Count == 4)
                        {
                            _isDrawingRegion = false;
                            pictureBox1.Cursor = Cursors.Default;
                            SetNumericControlsFromRegion(_regionPointsNorm);
                        }
                    }
                }
            }
            catch { }
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (pictureBox1?.Image == null) return;
                var pts = MapRegionToPictureBoxPoints(pictureBox1, _regionPointsNorm);
                if (pts != null && pts.Length > 0)
                {
                    using (var pen = new Pen(Color.Lime, 2))
                    using (var brush = new SolidBrush(Color.Lime))
                    {
                        for (int i = 0; i < pts.Length; i++)
                        {
                            e.Graphics.FillEllipse(brush, pts[i].X - 3, pts[i].Y - 3, 6, 6);
                            if (i > 0)
                                e.Graphics.DrawLine(pen, pts[i - 1], pts[i]);
                        }
                        if (pts.Length == 4)
                            e.Graphics.DrawLine(pen, pts[3], pts[0]);
                    }
                }
            }
            catch { }
        }

        // ==================== Region Drawing helpers ====================
        private static PointF? MapMouseToImageNormalized(PictureBox pb, Point m)
        {
            try
            {
                if (pb?.Image == null) return null;
                var img = pb.Image;
                float imageAspect = img.Width / (float)img.Height;
                float boxAspect = pb.ClientSize.Width / (float)pb.ClientSize.Height;
                float scale;
                RectangleF dest;
                if (imageAspect > boxAspect)
                {
                    scale = pb.ClientSize.Width / (float)img.Width;
                    float displayHeight = img.Height * scale;
                    float offsetY = (pb.ClientSize.Height - displayHeight) / 2f;
                    dest = new RectangleF(0, offsetY, pb.ClientSize.Width, displayHeight);
                }
                else
                {
                    scale = pb.ClientSize.Height / (float)img.Height;
                    float displayWidth = img.Width * scale;
                    float offsetX = (pb.ClientSize.Width - displayWidth) / 2f;
                    dest = new RectangleF(offsetX, 0, displayWidth, pb.ClientSize.Height);
                }
                if (!dest.Contains(m)) return null;
                float ix = (m.X - dest.X) / scale;
                float iy = (m.Y - dest.Y) / scale;
                ix = Math.Max(0, Math.Min(img.Width - 1, ix));
                iy = Math.Max(0, Math.Min(img.Height - 1, iy));
                // Trả về toạ độ pixel của ảnh (KHÔNG chuẩn hoá)
                return new PointF(ix, iy);
            }
            catch { return null; }
        }

        private static Point[] MapRegionToPictureBoxPoints(PictureBox pb, List<PointF> normPts)
        {
            try
            {
                if (pb?.Image == null || normPts == null || normPts.Count == 0) return Array.Empty<Point>();
                var img = pb.Image;
                float imageAspect = img.Width / (float)img.Height;
                float boxAspect = pb.ClientSize.Width / (float)pb.ClientSize.Height;
                float scale;
                RectangleF dest;
                if (imageAspect > boxAspect)
                {
                    scale = pb.ClientSize.Width / (float)img.Width;
                    float displayHeight = img.Height * scale;
                    float offsetY = (pb.ClientSize.Height - displayHeight) / 2f;
                    dest = new RectangleF(0, offsetY, pb.ClientSize.Width, displayHeight);
                }
                else
                {
                    scale = pb.ClientSize.Height / (float)img.Height;
                    float displayWidth = img.Width * scale;
                    float offsetX = (pb.ClientSize.Width - displayWidth) / 2f;
                    dest = new RectangleF(offsetX, 0, displayWidth, pb.ClientSize.Height);
                }
                var pts = new List<Point>(normPts.Count);
                foreach (var p in normPts)
                {
                    // p là toạ độ pixel ảnh
                    float ix = p.X;
                    float iy = p.Y;
                    int dx = (int)Math.Round(dest.X + ix * scale);
                    int dy = (int)Math.Round(dest.Y + iy * scale);
                    pts.Add(new Point(dx, dy));
                }
                return pts.ToArray();
            }
            catch { return Array.Empty<Point>(); }
        }

        private void SetNumericControlsFromRegion(List<PointF> normPts)
        {
            try
            {
                if (normPts == null || normPts.Count < 4) return;
                num_x1.Value = (decimal)Math.Round(normPts[0].X, 2);
                num_y1.Value = (decimal)Math.Round(normPts[0].Y, 2);
                num_x2.Value = (decimal)Math.Round(normPts[1].X, 2);
                num_y2.Value = (decimal)Math.Round(normPts[1].Y, 2);
                num_x3.Value = (decimal)Math.Round(normPts[2].X, 2);
                numericUpDown3.Value = (decimal)Math.Round(normPts[2].Y, 2); // y3
                numericUpDown6.Value = (decimal)Math.Round(normPts[3].X, 2); // x4
                numericUpDown5.Value = (decimal)Math.Round(normPts[3].Y, 2); // y4
            }
            catch { }
        }

        private List<PointF> GetRegionFromNumericControls()
        {
            try
            {
                var pts = new List<PointF>(4)
                {
                    new PointF((float)num_x1.Value, (float)num_y1.Value),
                    new PointF((float)num_x2.Value, (float)num_y2.Value),
                    new PointF((float)num_x3.Value, (float)numericUpDown3.Value),
                    new PointF((float)numericUpDown6.Value, (float)numericUpDown5.Value)
                };
                return pts;
            }
            catch { return null; }
        }

        private void SaveRegionToDatabase(int stt, List<PointF> ptsNorm)
        {
            if (ptsNorm == null || ptsNorm.Count < 4) return;
            using (var conn = new MySqlConnection(ClassSystemConfig.Ins?.m_ClsCommon?.connectionString))
            {
                conn.Open();
                string sql = @"UPDATE camera_list SET 
                                x1=@x1, y1=@y1,
                                x2=@x2, y2=@y2,
                                x3=@x3, y3=@y3,
                                x4=@x4, y4=@y4
                              WHERE STT=@STT";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@STT", stt);
                    // Lưu THEO PIXEL ảnh (không chuẩn hoá)
                    cmd.Parameters.AddWithValue("@x1", Math.Round(ptsNorm[0].X, 2));
                    cmd.Parameters.AddWithValue("@y1", Math.Round(ptsNorm[0].Y, 2));
                    cmd.Parameters.AddWithValue("@x2", Math.Round(ptsNorm[1].X, 2));
                    cmd.Parameters.AddWithValue("@y2", Math.Round(ptsNorm[1].Y, 2));
                    cmd.Parameters.AddWithValue("@x3", Math.Round(ptsNorm[2].X, 2));
                    cmd.Parameters.AddWithValue("@y3", Math.Round(ptsNorm[2].Y, 2));
                    cmd.Parameters.AddWithValue("@x4", Math.Round(ptsNorm[3].X, 2));
                    cmd.Parameters.AddWithValue("@y4", Math.Round(ptsNorm[3].Y, 2));
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }

}

