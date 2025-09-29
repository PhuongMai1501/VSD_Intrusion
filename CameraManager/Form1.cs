using LittleForker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Threading;
using System.IO;
using CameraManager.Class;
using System.Diagnostics;

namespace CameraManager
{
    public partial class Form1 : Form
    {
        #region Camera System Variables
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // Dynamic camera count based on database (cap at 6 live cameras)
        private int NumCameras => Math.Min(6, Math.Max(1, ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam?.Count ?? 6));
        // Optional override for active camera count (null = use NumCameras)
        private int? _cameraCountOverride = null; // default to full camera grid (up to 6)
        private int ActiveCameraCount => Math.Max(1, Math.Min(_cameraCountOverride ?? NumCameras, NumCameras));

        private readonly List<ProcessSupervisor> _supervisors = new List<ProcessSupervisor>();
        private readonly List<MemoryMappedFile> _mmfs = new List<MemoryMappedFile>();
        private readonly List<Mutex> _mutexes = new List<Mutex>();

        private readonly List<PictureBox> _pictureboxes = new List<PictureBox>();
        private volatile bool _isShuttingDown = false;

        private const int MaxFrameWidth = 3840;
        private const int MaxFrameHeight = 2160;
        private const long MaxFrameSize = (long)MaxFrameWidth * MaxFrameHeight * 3;

        // FPS Configuration
        // Dynamic Layout Variables
        private TableLayoutPanel tableLayoutPanelCamera;
        private TableLayoutPanel[] tableLayoutPanelDevice;
        public int Row = 2; // default rows for 6-camera grid
        public int Col = 3; // default columns for 6-camera grid

        // Fullscreen state tracking
        private bool _isFullscreen = false;
        private int _fullscreenCameraIndex = -1;
        private readonly object _fullscreenLock = new object();

        // Intrusion API mode (reference ProcessVideoTest flow)
        private const bool INTRUSION_API_MODE = true; // enable new flow using track_id
        // Base URL; ActionRecognitionClient will append "/detect"
        //private const string INTRUSION_API_BASE_URL = "http://localhost:5001"; // update if needed
        private const string INTRUSION_API_BASE_URL = "http://192.168.210.250:5001"; // in INFINIQ
        private readonly object _intrusionClientsLock = new object();
        private readonly System.Collections.Generic.Dictionary<int, ActionRecognitionClient> _intrusionClientsByCam = new System.Collections.Generic.Dictionary<int, ActionRecognitionClient>();

        // Detection overlay
        private readonly List<List<Detection>> _cameraDetections = new List<List<Detection>>();
        private readonly System.Windows.Forms.Timer _detectionTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _detectionCleanupTimer = new System.Windows.Forms.Timer();

        // Draw all detections using model's confidence

        // Store frame for AI detection processing
        private readonly Dictionary<int, Bitmap> _latestFrames = new Dictionary<int, Bitmap>();
        private readonly object _frameStoreLock = new object();
        // Track monotonically increasing frame sequence per camera to drop stale detections
        private readonly Dictionary<int, long> _frameSeqByCam = new Dictionary<int, long>();
        // Track last received frame time for No-Signal overlay
        private readonly Dictionary<int, DateTime> _lastFrameAt = new Dictionary<int, DateTime>();
        private const int NO_SIGNAL_TIMEOUT_MS = 2000;
        // Map UI index -> STT and restart control
        private readonly Dictionary<int, DateTime> _lastRestartAt = new Dictionary<int, DateTime>();
        private const int RESTART_STALL_MS = 7000;     // if no frame > 7s, consider stalled
        private const int RESTART_COOLDOWN_MS = 10000; // min 10s between restarts per camera

        // Detection processing flags to prevent recursion
        private readonly HashSet<int> _processsingDetection = new HashSet<int>();
        private readonly object _detectionProcessLock = new object();

        // Detection throttling and concurrency
        private const bool ENABLE_AI_DETECTION = true; // enable AI detection for intrusion API
        private readonly SemaphoreSlim _detectionConcurrency = new SemaphoreSlim(6); // allow up to 6 cameras concurrently

        // ==== DEBUG BBOX FLAGS ====
        private const bool DEBUG_DISABLE_TRACK_HANGOVER = false; // enable track flow with 3-frame ghost suppression
        private const bool DEBUG_CLEAR_PICTUREBOX_BEFORE_DRAW = false; // disable clear in production to avoid flicker
        private const bool DEBUG_VERBOSE_BBOX_LOG = true; // detailed logging for bbox lifecycle
        private const bool DEBUG_DETAILED_BBOX_TIMELINE = true; // log riêng: nhận API vs vẽ theo thời gian thực

        // UI toggle state
        // Toggle state and original layout widths for panelLog
        private bool _isLogCollapsed = false;
        private float _origCol0Width = 20f;
        private float _origCol1Width = 80f;

        // Alert/Confirm state per camera
        private class CameraAlertState
        {
            public bool Active;
            public bool BlinkOn;
            public DateTime LastBlinkAt;
            public DateTime LastAlarmAt;
            public string Label;
            public DateTime LastPopupAt;
        }
        private readonly Dictionary<int, CameraAlertState> _alertsByCam = new Dictionary<int, CameraAlertState>();
        private readonly System.Windows.Forms.Timer _alertTimer = new System.Windows.Forms.Timer();
        private DKVN.FormConfirmVision _confirmDialog;
        
        // Alarm throttling: avoid spamming alerts
        private readonly object _alarmLock = new object();
        private DateTime _lastAlarmSentAt = DateTime.MinValue;
        private const int ALARM_MIN_INTERVAL_MS = 10000; // 10 seconds
        private readonly Dictionary<int, DateTime> _lastDetectAt = new Dictionary<int, DateTime>();
        private const int DETECT_MIN_INTERVAL_MS = 100; // per-camera min interval (≈10 FPS); adjust down to 50 for ~20 FPS
        private const int DETECTION_TIMER_INTERVAL_MS = 80; // lịch detect
        private const int DETECTION_TTL_MS = 800; // giữ bbox lâu hơn để giảm nháy giữa các lượt detect
        private const int DRAW_TTL_MS = 600; // tăng giữ bbox một chút cho mượt
        private const int DETECTION_MAX_FRAME_LAG = 3; // nới lệch khung cho ổn định hơn
        // DB log throttling per camera to avoid spamming
        private readonly object _dbLogLock = new object();
        private readonly Dictionary<int, DateTime> _lastDbLogAtByCam = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, string> _lastDbEventByCam = new Dictionary<int, string>();
        private const int LOG_MIN_INTERVAL_MS = 10000; // 10s between identical logs per camera
        // Region overlay (from DB) per camera
        private class RegionData
        {
            public List<PointF> Points { get; set; } = new List<PointF>(4);
            public bool IsNormalized { get; set; } = true; // true if [0..1]
        }
        private readonly Dictionary<int, RegionData> _regionDataByCam = new Dictionary<int, RegionData>();
        private readonly object _regionLock = new object();

        // Track smoothing to reduce flicker
        private class TrackState
        {
            public double x1, y1, x2, y2;
            public string label;
            public double score;
            public DateTime lastSeen;
            public long lastFrameSeq;
            // Track frames since last API update for this track
            public int noUpdateFrames = 0;
            // Keep last few API-updated coords to detect stagnation
            public Queue<(double x1, double y1, double x2, double y2)> lastCoords = new Queue<(double, double, double, double)>();
        }
        private readonly object _trackLock = new object();
        private readonly Dictionary<int, Dictionary<int, TrackState>> _trackStates = new Dictionary<int, Dictionary<int, TrackState>>();
        private const int TRACK_HANGOVER_MS = 1000; // mượt hơn khi miss tạm (tăng nhẹ)
        private const int TRACK_MAX_AGE_MS = 2200;  // dọn track cũ sau ~2.2s không thấy (tăng nhẹ)
        private const int TRACK_HANGOVER_MAX_SAME_FRAMES = 3; // cho phép trùng toạ độ tối đa 3 frame gần nhất
        // Re-associate unstable API track_id to previous local track by IoU to avoid ID nhấp nháy
        private const double TRACK_REASSIGN_IOU = 0.55;          // ngưỡng IoU để coi là cùng đối tượng
        private const int TRACK_REASSIGN_MAX_AGE_MS = 700;       // chỉ xét track vừa được thấy gần đây

        // Draw/Filter thresholds
        private const double MIN_DRAW_SCORE = 0.05; // hạ ngưỡng để không bỏ sót bbox
        private const bool FILTER_WEAK_BOXES = false; // không bỏ qua bbox yếu để tránh hụt phát hiện
        private const bool ENABLE_STALE_WEAK_FILTER = true; // giữ phát hiện stale để tránh buffer lặp

        // Detect potential stale API responses (same empty-label boxes repeating)
        private class ApiStaleState
        {
            public string Signature;
            public DateTime LastAt;
            public int Repeat;
        }
        private readonly Dictionary<int, ApiStaleState> _apiStaleByCam = new Dictionary<int, ApiStaleState>();
        // Detection input config: use square input size from global config
        private int DETECT_INPUT_SIZE => INTRUSION_API_MODE ? 640 : Math.Max(1, ClassSystemConfig.Ins?.m_ClsCommon?.DetectionInputSize ?? 1280);
        private const long JPEG_QUALITY = 75L; // JPEG quality for request payload

        // MySQL Connection
        MySqlConnection connection = new MySqlConnection(ClassSystemConfig.Ins.m_ClsCommon.connectionString);

        #endregion

        public Form1()
        {
            InitializeComponent();

            // Capture original widths and wire label click to toggle log panel
            try
            {
                if (tableLayoutPanel2?.ColumnStyles?.Count >= 2)
                {
                    _origCol0Width = (float)tableLayoutPanel2.ColumnStyles[0].Width;
                    _origCol1Width = (float)tableLayoutPanel2.ColumnStyles[1].Width;
                }
            }
            catch { }

            // Skip runtime initialization while opening in Designer
            if (IsInDesignMode()) return;

            // Enable keyboard events
            this.KeyPreview = true;
            this.WindowState = FormWindowState.Maximized;

            // Show startup loading UI
            ClassSystemConfig.Ins.m_ClsCommon.StartupLoadingForm();

            // Handle process exit events to ensure cleanup
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ApplicationExit += Application_ApplicationExit;

            // Handle Windows shutdown/logoff
            Microsoft.Win32.SystemEvents.SessionEnding += SystemEvents_SessionEnding;

            // Initialize detection components
            InitializeDetectionSystem();

            // Initialize alert timer (blink + resend messages)
            try
            {
                _alertTimer.Interval = 1000; // 1s blink
                _alertTimer.Tick += AlertTimer_Tick;
                _alertTimer.Start();
            }
            catch { }
        }

        private void AlertTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                foreach (var kv in _alertsByCam.ToList())
                {
                    int cam = kv.Key;
                    var st = kv.Value;
                    if (!st.Active) continue;

                    // Toggle blink each tick
                    st.BlinkOn = !st.BlinkOn;
                    st.LastBlinkAt = now;

                    // Repaint this camera
                    if (cam >= 0 && cam < _pictureboxes.Count)
                    {
                        try { _pictureboxes[cam]?.Invalidate(); } catch { }
                    }

                    // Resend alarm message every 10s while unconfirmed
                    if ((now - st.LastAlarmAt).TotalMilliseconds >= ALARM_MIN_INTERVAL_MS && !string.IsNullOrWhiteSpace(st.Label))
                    {
                        _ = SendAlarmToActiveRecipientsAsync(st.Label);
                        st.LastAlarmAt = now;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(AlertTimer_Tick));
            }
        }

        private void label1_Click(object? sender, EventArgs e)
        {
            try
            {
                if (tableLayoutPanel2 == null || panelMain == null || panelLog == null) return;

                tableLayoutPanel2.SuspendLayout();

                if (!_isLogCollapsed)
                {
                    // Collapse log panel and let main span full width
                    panelLog.Visible = false;
                    if (tableLayoutPanel2.ColumnStyles.Count >= 2)
                    {
                        tableLayoutPanel2.ColumnStyles[0].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[0].Width = 0f;
                        tableLayoutPanel2.ColumnStyles[1].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[1].Width = 100f;
                    }
                    try
                    {
                        tableLayoutPanel2.SetColumn(panelMain, 0);
                        tableLayoutPanel2.SetColumnSpan(panelMain, 2);
                    }
                    catch { }
                    _isLogCollapsed = true;
                }
                else
                {
                    // Restore original layout
                    try
                    {
                        tableLayoutPanel2.SetColumnSpan(panelMain, 1);
                        tableLayoutPanel2.SetColumn(panelMain, 1);
                    }
                    catch { }

                    if (tableLayoutPanel2.ColumnStyles.Count >= 2)
                    {
                        tableLayoutPanel2.ColumnStyles[0].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[0].Width = _origCol0Width;
                        tableLayoutPanel2.ColumnStyles[1].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[1].Width = _origCol1Width;
                    }
                    panelLog.Visible = true;
                    _isLogCollapsed = false;
                }

                tableLayoutPanel2.ResumeLayout(true);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(label1_Click));
            }
        }

        private static bool IsInDesignMode()
        {
            try
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;
                var proc = System.Diagnostics.Process.GetCurrentProcess()?.ProcessName;
                return string.Equals(proc, "devenv", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(proc, "Blend", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        #region Form Event Handlers
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                if (IsInDesignMode()) return;
                Console.WriteLine("\n=== CameraManager Form1_Load Started ===");

                InitializeUI();
                LoadCameraList();
                // Thresholds now use global defaults (no per-camera DB)

                Console.WriteLine($"Camera Configuration: {ActiveCameraCount} cameras, {Row}x{Col} grid");

                LayoutCameraSpreadView();
                UpdateCameraLogInvoke(this);
                Thread.Sleep(1000);
                Console.WriteLine("? Form1_Load completed");
                ClassSystemConfig.Ins.m_ClsCommon.StopStartupLoadingForm();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Form1_Load error: {ex.Message}");
                FileLogger.LogException(ex, "Form1_Load");
            }
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            if (_isShuttingDown) return;

            try
            {
                int maxDraw = Math.Min(ActiveCameraCount, Math.Min(_mmfs.Count, _pictureboxes.Count));
                for (int i = 0; i < maxDraw; i++)
                {
                    UpdatePictureBox(i);
                    UpdateNoSignalOverlay(i);
                }
            }
            catch (Exception ex)
            {
                if (!_isShuttingDown)
                {
                    FileLogger.LogException(ex, "DisplayTimer_Tick");
                }
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                FileLogger.Log($"?? Key pressed: {e.KeyCode}, Fullscreen: {_isFullscreen}, Camera: {_fullscreenCameraIndex}");
                Console.WriteLine($"?? Key pressed: {e.KeyCode}, Fullscreen: {_isFullscreen}, Camera: {_fullscreenCameraIndex}");

                // Handle ESC first - always exit fullscreen if active
                if (e.KeyCode == Keys.Escape)
                {
                    if (_isFullscreen)
                    {
                        FileLogger.Log("?? ESC pressed - Forcing exit fullscreen mode");
                        Console.WriteLine("?? ESC pressed - Forcing exit fullscreen mode");

                        lock (_fullscreenLock)
                        {
                            ExitFullscreen();
                        }
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        FileLogger.Log("?? ESC pressed but not in fullscreen mode");
                        Console.WriteLine("?? ESC pressed but not in fullscreen mode");
                    }
                    e.Handled = true;
                    return;
                }

                // Handle F1-F12 keys for camera selection
                else if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12)
                {
                    int cameraIndex = e.KeyCode - Keys.F1;
                    if (cameraIndex < ActiveCameraCount)
                    {
                        FileLogger.Log($"?? F{cameraIndex + 1} pressed - Toggling camera {cameraIndex + 1}");
                        Console.WriteLine($"?? F{cameraIndex + 1} pressed - Toggling camera {cameraIndex + 1}");

                        lock (_fullscreenLock)
                        {
                            ToggleFullscreen(cameraIndex);
                        }
                        e.Handled = true;
                    }
                    else
                    {
                        Console.WriteLine($"?? F{cameraIndex + 1} pressed but only {ActiveCameraCount} cameras available");
                    }
                }

                // Handle number keys 1-9 for camera selection
                else if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
                {
                    int cameraIndex = e.KeyCode - Keys.D1;

                    if (cameraIndex < ActiveCameraCount)
                    {
                        FileLogger.Log($"?? Number {cameraIndex + 1} pressed - Toggling camera {cameraIndex + 1}");
                        Console.WriteLine($"?? Number {cameraIndex + 1} pressed - Toggling camera {cameraIndex + 1}");

                        lock (_fullscreenLock)
                        {
                            if (_isFullscreen && _fullscreenCameraIndex == cameraIndex)
                            {
                                Console.WriteLine($"?? Camera {cameraIndex + 1} already in fullscreen - exiting");
                                ExitFullscreen();
                            }
                            else
                            {
                                Console.WriteLine($"?? Switching to Camera {cameraIndex + 1} fullscreen");
                                EnterFullscreen(cameraIndex);
                            }
                        }
                        e.Handled = true;
                    }
                    else
                    {
                        Console.WriteLine($"?? Number {cameraIndex + 1} pressed but only {ActiveCameraCount} cameras available");
                        Console.WriteLine($"?? Available cameras: 1-{ActiveCameraCount} (Press F1-F{ActiveCameraCount} or 1-{Math.Min(ActiveCameraCount, 9)})");
                    }
                }

                // Handle numpad keys 1-9 for camera selection
                else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
                {
                    int cameraIndex = e.KeyCode - Keys.NumPad1;

                    if (cameraIndex < ActiveCameraCount)
                    {
                        FileLogger.Log($"?? Numpad {cameraIndex + 1} pressed - Toggling camera {cameraIndex + 1}");
                        Console.WriteLine($"?? Numpad {cameraIndex + 1} pressed - Toggling camera {cameraIndex + 1}");

                        lock (_fullscreenLock)
                        {
                            if (_isFullscreen && _fullscreenCameraIndex == cameraIndex)
                            {
                                Console.WriteLine($"?? Camera {cameraIndex + 1} already in fullscreen - exiting");
                                ExitFullscreen();
                            }
                            else
                            {
                                Console.WriteLine($"?? Switching to Camera {cameraIndex + 1} fullscreen");
                                EnterFullscreen(cameraIndex);
                            }
                        }
                        e.Handled = true;
                    }
                    else
                    {
                        Console.WriteLine($"?? Numpad {cameraIndex + 1} pressed but only {ActiveCameraCount} cameras available");
                    }
                }

                // Show help with H key
                else if (e.KeyCode == Keys.H || (e.Control && e.KeyCode == Keys.H))
                {
                    ShowKeyboardShortcuts();
                    e.Handled = true;
                }

                // (Removed) Test detection overlay via T key

                // Emergency shutdown with Ctrl+Alt+X
                else if (e.Control && e.Alt && e.KeyCode == Keys.X)
                {
                    FileLogger.Log("?? EMERGENCY SHUTDOWN HOTKEY PRESSED (Ctrl+Alt+X)");
                    EmergencyShutdown();
                    e.Handled = true;
                    Application.Exit();
                }

                // Force kill workers with Ctrl+Shift+K
                else if (e.Control && e.Shift && e.KeyCode == Keys.K)
                {
                    FileLogger.Log("?? FORCE KILL WORKERS HOTKEY PRESSED (Ctrl+Shift+K)");
                    ForceKillAllCameraWorkers();
                    e.Handled = true;
                }

                // Reload camera list with Ctrl+R
                else if (e.Control && e.KeyCode == Keys.R)
                {
                    FileLogger.Log("?? RELOAD CAMERA LIST HOTKEY PRESSED (Ctrl+R)");
                    ReloadCameraList();
                    e.Handled = true;
                }

                // Debug fullscreen state with Ctrl+D
                else if (e.Control && e.KeyCode == Keys.D)
                {
                    DebugFullscreenState();
                    e.Handled = true;
                }

                // Force exit fullscreen with Ctrl+E
                else if (e.Control && e.KeyCode == Keys.E)
                {
                    Console.WriteLine("?? FORCE EXIT FULLSCREEN HOTKEY PRESSED (Ctrl+E)");
                    FileLogger.Log("?? FORCE EXIT FULLSCREEN HOTKEY PRESSED (Ctrl+E)");
                    ForceExitFullscreen();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "Form1_KeyDown");
                Console.WriteLine($"? Form1_KeyDown error: {ex.Message}");
            }
        }
        private void btnAlarm_Click(object sender, EventArgs e)
        {
            ChangeButtonColorClick(sender);
            if (ClassSystemConfig.Ins.m_FrmConfigMessage == null || ClassSystemConfig.Ins.m_FrmConfigMessage.IsDisposed)
            {
                ClassSystemConfig.Ins.m_FrmConfigMessage = new FormConfigMessage();
            }
            ClassSystemConfig.Ins.m_FrmConfigMessage.Show();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Config Message View",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void btnLogView_Click(object sender, EventArgs e)
        {
            ChangeButtonColorClick(sender);
            ClassSystemConfig.Ins.m_FrmLogView.Show();
            ClassSystemConfig.Ins.m_FrmLogView.ShowOnScreen();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Log View",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        #endregion

        #region Button Event Handlers

        private void btnHideSetting_Click(object sender, EventArgs e)
        {
            try
            {
                panelSetting.Visible = !panelSetting.Visible;
                FileLogger.Log($"Setting panel visibility toggled: {panelSetting.Visible}");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnHideSetting_Click");
            }
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            try
            {
                this.WindowState = FormWindowState.Minimized;
                FileLogger.Log("Window minimized");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnMinimize_Click");
            }
        }

        private void btnMaximize_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Normal;
                    FileLogger.Log("Window restored to normal");
                }
                else
                {
                    this.WindowState = FormWindowState.Maximized;
                    FileLogger.Log("Window maximized");
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnMaximize_Click");
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            try
            {
                FileLogger.Log("Exit button clicked - Closing application");
                this.Close();
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnExit_Click");
            }
        }

        private void btnStartCamera_Click(object sender, EventArgs e)
        {
            try
            {
                FileLogger.Log("?? Start Camera button clicked");
                ClassSystemConfig.Ins.m_ClsCommon.StartLoadingForm();

                btnStartCamera.Enabled = false;
                this.Cursor = Cursors.WaitCursor;

                StartCameraSystem();

                btnStopCamera.Enabled = true;
                this.Cursor = Cursors.Default;

                FileLogger.Log("? Camera system start initiated");
                ClassSystemConfig.Ins.m_ClsCommon.StopLoadingForm();
                MessageBox.Show("Camera system started successfully!", "Start Camera",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnStartCamera_Click");

                btnStartCamera.Enabled = true;
                btnStopCamera.Enabled = false;
                this.Cursor = Cursors.Default;

                MessageBox.Show($"Error starting camera system: {ex.Message}", "Start Camera Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStopCamera_Click(object sender, EventArgs e)
        {
            try
            {
                FileLogger.Log("?? Stop Camera button clicked");

                btnStopCamera.Enabled = false;
                btnStartCamera.Enabled = false;
                this.Cursor = Cursors.WaitCursor;

                DisplayTimer?.Stop();
                _detectionTimer?.Stop();
                _detectionCleanupTimer?.Stop();

                lock (_cameraDetections)
                {
                    for (int i = 0; i < _cameraDetections.Count; i++)
                    {
                        _cameraDetections[i].Clear();
                    }
                }

                lock (_frameStoreLock)
                {
                    foreach (var frame in _latestFrames.Values)
                    {
                        try { frame?.Dispose(); } catch { }
                    }
                    _latestFrames.Clear();
                }

                foreach (var pictureBox in _pictureboxes)
                {
                    try
                    {
                        if (!pictureBox.IsDisposed)
                        {
                            var oldImage = pictureBox.Image;
                            pictureBox.Image = null;
                            oldImage?.Dispose();

                            if (pictureBox.Controls.Count > 0)
                            {
                                pictureBox.Controls[0].Visible = true;
                            }

                            pictureBox.Invalidate();
                        }
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException(ex, $"Clear PictureBox");
                    }
                }

                EmergencyShutdown();

                this.Cursor = Cursors.Default;
                btnStartCamera.Enabled = true;
                btnStopCamera.Enabled = false;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                FileLogger.Log("?? Camera system successfully stopped");
                MessageBox.Show("Camera system stopped successfully!", "Stop Camera",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnStopCamera_Click");

                this.Cursor = Cursors.Default;
                btnStartCamera.Enabled = true;
                btnStopCamera.Enabled = false;

                MessageBox.Show($"Error stopping camera system: {ex.Message}", "Stop Camera Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSetting_Click(object sender, EventArgs e)
        {
            try
            {
                ChangeButtonColorClick(sender);
                ClassSystemConfig.Ins.m_FrmParamCamera.Show();
                ClassSystemConfig.Ins.m_FrmParamCamera.ShowOnScreen();
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                        "Clicked Camera Setting",
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                FileLogger.Log("Camera settings opened");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "btnSetting_Click");
            }
        }

        private void ChangeButtonColorClick(object sender)
        {
            try
            {
                if ((Button)sender == btnAlarm)
                {
                    btnAlarm.BackColor = Color.DeepSkyBlue;
                    btnSetting.BackColor = Color.Aquamarine;
                    btnLogView.BackColor = Color.Aquamarine;
                }
                else if ((Button)sender == btnSetting)
                {
                    btnAlarm.BackColor = Color.Aquamarine;
                    btnSetting.BackColor = Color.DeepSkyBlue;
                    btnLogView.BackColor = Color.Aquamarine;
                }
                else if ((Button)sender == btnLogView)
                {
                    btnAlarm.BackColor = Color.Aquamarine;
                    btnSetting.BackColor = Color.Aquamarine;
                    btnLogView.BackColor = Color.DeepSkyBlue;
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ChangeButtonColorClick");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _isShuttingDown = true;
                FileLogger.Log("\n=== Application Shutdown Started ===");

                DisplayTimer?.Stop();
                _detectionTimer?.Stop();
                _detectionTimer?.Dispose();
                _detectionCleanupTimer?.Stop();
                _detectionCleanupTimer?.Dispose();
                try
                {
                    lock (_intrusionClientsLock)
                    {
                        foreach (var kv in _intrusionClientsByCam)
                        {
                            try { kv.Value?.Dispose(); } catch { }
                        }
                        _intrusionClientsByCam.Clear();
                    }
                }
                catch { }

                lock (_cameraDetections)
                {
                    for (int i = 0; i < _cameraDetections.Count; i++)
                    {
                        _cameraDetections[i].Clear();
                    }
                }

                lock (_frameStoreLock)
                {
                    foreach (var frame in _latestFrames.Values)
                    {
                        try { frame?.Dispose(); } catch { }
                    }
                    _latestFrames.Clear();
                }

                ForceKillAllCameraWorkers();

                foreach (var supervisor in _supervisors)
                {
                    try { supervisor?.Dispose(); } catch { }
                }

                foreach (var mmf in _mmfs)
                {
                    try { mmf?.Dispose(); } catch { }
                }
                foreach (var mutex in _mutexes)
                {
                    try { mutex?.Close(); mutex?.Dispose(); } catch { }
                }

                foreach (var pictureBox in _pictureboxes)
                {
                    try { pictureBox?.Image?.Dispose(); } catch { }
                }

                FileLogger.Log("? Application shutdown completed successfully");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "Form1_FormClosing");
            }
        }

        #endregion

        #region AI Detection Classes
        public class Detection
        {
            public string label { get; set; }
            public double x1 { get; set; }
            public double y1 { get; set; }
            public double x2 { get; set; }
            public double y2 { get; set; }
            public double score { get; set; }
            public DateTime timestamp { get; set; } = DateTime.Now;
            // Optional: track id when using intrusion API
            public int? track_id { get; set; }
            // Frame sequence index associated with this detection (for ghost filtering)
            public long frame_seq { get; set; }
            // 0 = từ API frame hiện tại; >0 = số frame treo (hangover) khi không có API update
            public int hangover_frames { get; set; } = 0;
        }

        // Intrusion API DTOs moved to CameraManager.Class (IntrusionDtos.cs)

        private void InitializeDetectionSystem()
        {
            try
            {
                // Do not assume camera count at startup; cameras load later.
                EnsureCameraDetectionsSize(Math.Max(1, ActiveCameraCount));

                if (ENABLE_AI_DETECTION)
                {
                    _detectionTimer.Interval = DETECTION_TIMER_INTERVAL_MS;
                    _detectionTimer.Tick += DetectionTimer_Tick;
                    _detectionTimer.Start();

                    _detectionCleanupTimer.Interval = 60; // faster cleanup for 6-camera setup
                    _detectionCleanupTimer.Tick += DetectionCleanupTimer_Tick;
                    _detectionCleanupTimer.Start();

                    FileLogger.Log("? AI Detection system initialized");
                }
                else
                {
                    FileLogger.Log("? AI Detection disabled (stream only)");
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "InitializeDetectionSystem");
            }
        }

        private void EnsureCameraDetectionsSize(int target)
        {
            try
            {
                if (target <= 0) target = 1;
                lock (_cameraDetections)
                {
                    while (_cameraDetections.Count < target)
                    {
                        _cameraDetections.Add(new List<Detection>());
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "EnsureCameraDetectionsSize");
            }
        }

        private void DetectionCleanupTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isShuttingDown) return;

                var currentTime = DateTime.Now;

                lock (_cameraDetections)
                {
                    EnsureCameraDetectionsSize(Math.Max(ActiveCameraCount, _pictureboxes.Count));
                    for (int cameraIndex = 0; cameraIndex < _cameraDetections.Count; cameraIndex++)
                    {
                        var detections = _cameraDetections[cameraIndex];
                        int originalCount = detections.Count;

                        detections.RemoveAll(d => (currentTime - d.timestamp).TotalMilliseconds > DETECTION_TTL_MS);

                        if (detections.Count != originalCount)
                        {
                            int removed = originalCount - detections.Count;
                            if (DEBUG_VERBOSE_BBOX_LOG)
                            {
                                try { FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: cleanup removed={removed} remain={detections.Count}"); } catch { }
                            }
                            TriggerPaintEvent(cameraIndex, detections.Count);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "DetectionCleanupTimer_Tick");
            }
        }

        private async void DetectionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isShuttingDown) return;
                // Schedule detection tasks per camera with throttling and concurrency limits
                var now = DateTime.Now;
                int maxIndex;
                lock (_frameStoreLock) { maxIndex = _latestFrames.Count; }

                for (int i = 0; i < maxIndex; i++)
                {
                    Bitmap frame = null;
                    long grabbedSeq = 0;
                    lock (_frameStoreLock)
                    {
                        if (!_latestFrames.ContainsKey(i)) continue;
                        frame = (Bitmap)_latestFrames[i].Clone();
                        _frameSeqByCam.TryGetValue(i, out grabbedSeq);
                    }

                    bool shouldProcess = false;
                    lock (_detectionProcessLock)
                    {
                        if (!_processsingDetection.Contains(i))
                        {
                            if (!_lastDetectAt.TryGetValue(i, out var last) || (now - last).TotalMilliseconds >= DETECT_MIN_INTERVAL_MS)
                            {
                                _processsingDetection.Add(i);
                                _lastDetectAt[i] = now;
                                shouldProcess = true;
                            }
                        }
                    }

                    if (!shouldProcess)
                    {
                        // Nếu không xử lý vòng này, kiểm tra và xoá nhanh bbox quá hạn để tránh bóng ma
                        try
                        {
                            lock (_cameraDetections)
                            {
                                if (i < _cameraDetections.Count && _cameraDetections[i].Count > 0)
                                {
                                    _cameraDetections[i].RemoveAll(d => (now - d.timestamp).TotalMilliseconds > DETECTION_TTL_MS);
                                    if (_cameraDetections[i].Count == 0)
                                    {
                                        TriggerPaintEvent(i, 0);
                                    }
                                }
                            }
                        }
                        catch { }
                        frame.Dispose();
                        continue;
                    }

                    _ = ProcessDetectionAsync(i, frame, grabbedSeq);
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "DetectionTimer_Tick");
            }
        }

        private async Task ProcessDetectionAsync(int cameraIndex, Bitmap frame, long frameSeq)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                // Limit global concurrency; skip if at capacity to keep realtime
                if (!await _detectionConcurrency.WaitAsync(0))
                {
                    frame.Dispose();
                    lock (_detectionProcessLock)
                    {
                        _processsingDetection.Remove(cameraIndex);
                    }
                    return;
                }

                // Resize frame to reduce payload
                using var resized = ResizeToSquare(frame, DETECT_INPUT_SIZE);

                List<Detection> detections;
                if (INTRUSION_API_MODE)
                {
                    detections = await DetectIntrusionAsync(cameraIndex, resized, frame.Width, frame.Height, DETECT_INPUT_SIZE, frameSeq) 
                                 ?? new List<Detection>();
                }

                // Logging for intrusion mode is handled inside DetectIntrusionAsync (only on real API results)
                sw.Stop();
                long apiLatencyMs = sw.ElapsedMilliseconds;
                long latestSeqForLog = 0;
                lock (_frameStoreLock)
                {
                    _frameSeqByCam.TryGetValue(cameraIndex, out latestSeqForLog);
                }
                try
                {
                    FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: detect latency={apiLatencyMs}ms frameSeq={frameSeq} latestSeq={latestSeqForLog} got={detections?.Count ?? 0}");
                }
                catch { }

                // Bỏ kết quả quá cũ, cho phép lệch tối đa vài frame để tránh drop do latency
                bool staleResult = false;
                lock (_frameStoreLock)
                {
                    if (_frameSeqByCam.TryGetValue(cameraIndex, out var latestSeq))
                    {
                        long lag = latestSeq - frameSeq;
                        if (lag > DETECTION_MAX_FRAME_LAG)
                        {
                            staleResult = true;
                        }
                    }
                }
                if (staleResult)
                {
                    try
                    {
                        // Bỏ qua gói stale, giữ nguyên buffer hiện tại để tránh giật nháy
                        if (DEBUG_VERBOSE_BBOX_LOG)
                        {
                            try { FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: Stale result. Ignore update; keep current draw buffer."); } catch { }
                        }
                    }
                    catch { }
                    return;
                }

                // Vẽ tất cả bbox ngay; giữ nguyên confidence của model
                var filtered = detections ?? new List<Detection>();

                // Ổn định: chỉ cập nhật buffer nếu có bbox hợp lệ; nếu rỗng thì giữ buffer cũ cho đến khi TTL dọn
                EnsureCameraDetectionsSize(cameraIndex + 1);
                if (filtered.Count > 0)
                {
                    // Sắp xếp ổn định theo track_id trước khi lưu để hạn chế đổi thứ tự
                    filtered = filtered
                        .OrderBy(d => d?.track_id ?? int.MaxValue)
                        .ThenBy(d => ((d?.x1 ?? 0) + (d?.x2 ?? 0)) * 0.5)
                        .ToList();

                    // Khử trùng lặp mạnh tay (IoU cao) trong cùng khung
                    filtered = DeduplicateOverlapping(filtered, 0.9);

                    lock (_cameraDetections)
                    {
                        if (cameraIndex < _cameraDetections.Count)
                        {
                            _cameraDetections[cameraIndex].Clear();
                            _cameraDetections[cameraIndex].AddRange(filtered);
                            if (DEBUG_VERBOSE_BBOX_LOG)
                            {
                                try { FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: store updated count={filtered.Count}"); } catch { }
                            }
                        }
                    }
                }

                // Cập nhật thời gian nhận detection gần nhất
                try
                {
                    lock (_cameraDetections)
                    {
                        // no separate dict needed; timestamps are on items
                    }
                }
                catch { }

                if (filtered.Count > 0)
                {
                    // Tạm thời không lưu ảnh khi có detection
                    // try { SaveDetectionImage(cameraIndex, frame, filtered); } catch (Exception exSave) { FileLogger.LogException(exSave, "ProcessDetectionAsync -> SaveDetectionImage"); }

                    // Gửi cảnh báo: dùng trực tiếp nhãn action đầu tiên (nếu có)
                    try
                    {
                        string eventTextRaw = filtered.FirstOrDefault(d => d != null && !string.IsNullOrWhiteSpace(d.label))?.label?.Trim();
                        // Only trigger confirm/alert if any labeled detection is inside region
                        bool anyInsideRegion = false;
                        if (!string.IsNullOrWhiteSpace(eventTextRaw))
                        {
                            try
                            {
                                var regionPts = GetRegionPointsNormalized(cameraIndex, frame.Width, frame.Height);
                                if (regionPts != null && regionPts.Length >= 3)
                                {
                                    foreach (var d in filtered)
                                    {
                                        if (d == null || string.IsNullOrWhiteSpace(d.label)) continue;
                                        double cx = (d.x1 + d.x2) / 2.0;
                                        double cy = (d.y1 + d.y2) / 2.0;
                                        if (IsPointInPolygonF(new PointF((float)cx, (float)cy), regionPts))
                                        {
                                            anyInsideRegion = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                        if (!string.IsNullOrWhiteSpace(eventTextRaw) && anyInsideRegion)
                        {
                            // Gửi cảnh báo với nhãn gốc (không đổi hoa)
                            _ = SendAlarmToActiveRecipientsAsync(eventTextRaw);
                            // Kích hoạt cảnh báo: viền đỏ nhấp nháy + popup confirm
                            try { ActivateCameraAlert(cameraIndex, eventTextRaw); } catch { }

                            // Ghi log DB khi có nhãn hợp lệ, có chống spam theo camera
                            bool allowDbLog = false;
                            var now = DateTime.Now;
                            string eventTextDb = eventTextRaw.ToUpperInvariant();
                            lock (_dbLogLock)
                            {
                                _lastDbLogAtByCam.TryGetValue(cameraIndex, out var lastAt);
                                _lastDbEventByCam.TryGetValue(cameraIndex, out var lastEvt);

                                bool labelChanged = string.IsNullOrWhiteSpace(lastEvt) || !string.Equals(lastEvt, eventTextDb, StringComparison.Ordinal);
                                bool timeoutPassed = lastAt == DateTime.MinValue || (now - lastAt).TotalMilliseconds >= LOG_MIN_INTERVAL_MS;

                                if (labelChanged || timeoutPassed)
                                {
                                    _lastDbLogAtByCam[cameraIndex] = now;
                                    _lastDbEventByCam[cameraIndex] = eventTextDb;
                                    allowDbLog = true;
                                }
                            }

                            if (allowDbLog)
                            {
                                try
                                {
                                    // Lưu DB với nhãn viết hoa theo yêu cầu
                                    AddCameraLogData(cameraIndex + 1, DateTime.Now, eventTextDb, null);
                                    try { UpdateCameraLogInvoke(this); } catch { }
                                }
                                catch (Exception exLog)
                                {
                                    FileLogger.LogException(exLog, "ProcessDetectionAsync -> AddCameraLogData");
                                }
                            }
                        }
                    }
                    catch (Exception exAlarm)
                    {
                        FileLogger.LogException(exAlarm, "ProcessDetectionAsync -> SendAlarm");
                    }
                }
                TriggerPaintEvent(cameraIndex, filtered.Count);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ProcessDetectionAsync");
            }
            finally
            {
                try { frame.Dispose(); } catch { }
                _detectionConcurrency.Release();
                lock (_detectionProcessLock)
                {
                    _processsingDetection.Remove(cameraIndex);
                }
            }
        }

        private ActionRecognitionClient GetIntrusionClient(int cameraIndex)
        {
            lock (_intrusionClientsLock)
            {
                if (_intrusionClientsByCam.TryGetValue(cameraIndex, out var client) && client != null)
                    return client;
                // Map by STT (cameraIndex+1) to ports 5001..5006
                // Example: CAM1->5001, CAM2->5002 ... CAM6->5006
                string baseUrl = INTRUSION_API_BASE_URL;
                try
                {
                    var uri = new Uri(baseUrl);
                    // Always start from 5001 regardless of provided port
                    int stt = cameraIndex + 1;
                    int port = 5000 + stt; // 5001..5006
                    var ub = new UriBuilder(uri.Scheme, uri.Host, port);
                    baseUrl = ub.Uri.ToString().TrimEnd('/');
                }
                catch { /* fallback: use baseUrl as-is */ }

                var newClient = new ActionRecognitionClient(baseUrl);
                _intrusionClientsByCam[cameraIndex] = newClient;
                return newClient;
            }
        }

        private async Task<List<Detection>> DetectIntrusionAsync(int cameraIndex, Bitmap squareFrame, int origW, int origH, int squareSize, long frameSeq)
        {
            try
            {
                var jpegBytes = EncodeJpeg(squareFrame, JPEG_QUALITY);
                string base64Image = Convert.ToBase64String(jpegBytes);

                // Use ActionRecognitionClient per camera to call intrusion API with sticky stream_id
                var client = GetIntrusionClient(cameraIndex);
                string streamId = $"cam_{cameraIndex + 1}";
                MultiPersonDetectionResponse obj = await client.DetectAsync(base64Image, streamId);
                if (obj == null)
                {
                    return new List<Detection>();
                }

                var list = new List<Detection>();
                if (obj?.Persons != null && obj.Persons.Count > 0)
                {
                    foreach (var p in obj.Persons)
                    {
                        if (p?.Bbox == null) continue;
                        double sx1 = p.Bbox.X1;
                        double sy1 = p.Bbox.Y1;
                        double sx2 = sx1 + p.Bbox.W;
                        double sy2 = sy1 + p.Bbox.H;

                        list.Add(new Detection
                        {
                            label = p.Action?.Action ?? string.Empty,
                            score = p.Action?.Confidence ?? 0,
                            x1 = sx1,
                            y1 = sy1,
                            x2 = sx2,
                            y2 = sy2,
                            track_id = p.TrackId,
                            timestamp = DateTime.Now,
                            frame_seq = frameSeq,
                            hangover_frames = 0
                        });
                    }
                }

                // Normalize to original frame coordinates [0..1]
                var normalized = NormalizeDetectionsToOriginalFrame(list, origW, origH, squareSize);

                // Stale-response detection: if API repeatedly returns identical boxes with empty/low-confidence labels, reset buffer once
                bool looksWeakStale = false;
                if (normalized != null && normalized.Count > 0 && ENABLE_STALE_WEAK_FILTER)
                {
                    bool allWeak = true;
                    var sb = new StringBuilder();
                    sb.Append(normalized.Count).Append("|");
                    for (int i = 0; i < normalized.Count; i++)
                    {
                        var d = normalized[i];
                        bool hasLabel = !string.IsNullOrWhiteSpace(d?.label);
                        double score = d?.score ?? 0;
                        if (hasLabel && score >= MIN_DRAW_SCORE) allWeak = false;
                        // signature: bbox rounded + label presence + coarse score bucket
                        int bucket = (int)Math.Floor(Math.Max(0, Math.Min(1.0, score)) * 10);
                        sb.AppendFormat("{0:F3},{1:F3},{2:F3},{3:F3},{4},{5};", d.x1, d.y1, d.x2, d.y2, hasLabel ? 1 : 0, bucket);
                    }
                    if (allWeak)
                    {
                        string sig = sb.ToString();
                        lock (_apiStaleByCam)
                        {
                            if (!_apiStaleByCam.TryGetValue(cameraIndex, out var st))
                            {
                                st = new ApiStaleState { Signature = sig, LastAt = DateTime.Now, Repeat = 1 };
                                _apiStaleByCam[cameraIndex] = st;
                            }
                            else
                            {
                                if (st.Signature == sig)
                                {
                                    st.Repeat++;
                                    st.LastAt = DateTime.Now;
                                }
                                else
                                {
                                    st.Signature = sig;
                                    st.Repeat = 1;
                                    st.LastAt = DateTime.Now;
                                }
                            }

                            // If same weak signature repeats >= 3 times within ~2s, treat as stale buffer
                            if (_apiStaleByCam[cameraIndex].Repeat >= 3 && (DateTime.Now - _apiStaleByCam[cameraIndex].LastAt).TotalSeconds <= 2.0)
                            {
                                looksWeakStale = true;
                                _apiStaleByCam[cameraIndex].Repeat = 0; // reset counter
                            }
                        }
                    }
                }

                if (looksWeakStale)
                {
                    try
                    {
                        FileLogger.Log($"DetectIntrusionAsync: Detected stale weak boxes for cam {cameraIndex + 1}, resetting server buffer");
                        var client2 = GetIntrusionClient(cameraIndex);
                        _ = client2.ResetServerBuffer();
                    }
                    catch { }
                    // Clear track states for this camera as well
                    lock (_trackLock)
                    {
                        _trackStates.Remove(cameraIndex);
                    }
                    return new List<Detection>();
                }

                // Log riêng: nhận kết quả từ API (sau normalize) để so sánh với lúc vẽ
                if (DEBUG_DETAILED_BBOX_TIMELINE)
                {
                    if (normalized != null && normalized.Count > 0)
                    {
                        foreach (var d in normalized)
                        {
                            try
                            {
                                FileLogger.Log($"[API] CAM {cameraIndex + 1} t={DateTime.Now:HH:mm:ss.fff} frameSeq={frameSeq} detSeq={d.frame_seq} id={(d.track_id?.ToString() ?? "-")} xy=({d.x1:F3},{d.y1:F3},{d.x2:F3},{d.y2:F3}) lbl='{d.label}' s={d.score:F2}");
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        try { FileLogger.Log($"[API] CAM {cameraIndex + 1} t={DateTime.Now:HH:mm:ss.fff} frameSeq={frameSeq} det=0"); } catch { }
                    }
                }
                if (DEBUG_VERBOSE_BBOX_LOG)
                {
                    try
                    {
                        FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: after normalize count={normalized.Count} timestamps=[{string.Join(", ", normalized.Select(d=>d.timestamp.ToString("HH:mm:ss.fff")))}]");
                    }
                    catch { }
                }

                // Merge with per-camera track states to smooth and add short hangover
                if (DEBUG_DISABLE_TRACK_HANGOVER)
                {
                    return normalized ?? new List<Detection>();
                }
                else
                {
                    var merged = UpdateAndBuildTracks(cameraIndex, normalized, frameSeq);
                    return merged;
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "DetectIntrusionAsync");
                return new List<Detection>();
            }
        }

        // Update track states with current detections (using track_id) and build a smoothed list to draw.
        private List<Detection> UpdateAndBuildTracks(int cameraIndex, List<Detection> current, long frameSeq)
        {
            var now = DateTime.Now;
            var result = new List<Detection>();
            try
            {
                Dictionary<int, TrackState> camTracks;
                lock (_trackLock)
                {
                    if (!_trackStates.TryGetValue(cameraIndex, out camTracks))
                    {
                        camTracks = new Dictionary<int, TrackState>();
                        _trackStates[cameraIndex] = camTracks;
                    }
                }

                // 0) Pre-pass: stabilize IDs. For detections with API id not found in camTracks,
                // try to re-associate to an existing recent track via IoU to avoid ID blinking.
                if (current != null && current.Count > 0)
                {
                    var claimed = new HashSet<int>(); // camTracks keys already matched this frame
                    // pre-mark keys that are already updated by their own id
                    lock (_trackLock)
                    {
                        foreach (var d in current)
                        {
                            if (d?.track_id != null && camTracks.ContainsKey(d.track_id.Value))
                            {
                                claimed.Add(d.track_id.Value);
                            }
                        }
                    }

                    foreach (var d in current)
                    {
                        if (d == null || d.track_id == null) continue;
                        int apiId = d.track_id.Value;
                        // If API id already exists in our map, no need to reassign
                        bool hasSame = false;
                        lock (_trackLock) { hasSame = camTracks.ContainsKey(apiId); }
                        if (hasSame) continue;

                        // Search best IoU match among recent tracks not already claimed
                        int bestKey = -1;
                        double bestIou = 0.0;
                        lock (_trackLock)
                        {
                            foreach (var kv in camTracks)
                            {
                                if (claimed.Contains(kv.Key)) continue; // already matched by another detection
                                var st = kv.Value;
                                double ageMs = (now - st.lastSeen).TotalMilliseconds;
                                if (ageMs > TRACK_REASSIGN_MAX_AGE_MS) continue; // too old to reliably match

                                // Compute IoU between current detection and stored track box
                                var temp = new Detection
                                {
                                    x1 = st.x1, y1 = st.y1, x2 = st.x2, y2 = st.y2, track_id = kv.Key
                                };
                                double iou = IoU(d, temp);
                                if (iou > bestIou)
                                {
                                    bestIou = iou;
                                    bestKey = kv.Key;
                                }
                            }
                        }

                        if (bestKey >= 0 && bestIou >= TRACK_REASSIGN_IOU)
                        {
                            // Re-map incoming detection to the best existing track key
                            d.track_id = bestKey;
                            claimed.Add(bestKey);
                            if (DEBUG_VERBOSE_BBOX_LOG)
                            {
                                try { FileLogger.Log($"[TRACK-REASSIGN] CAM {cameraIndex + 1} seq={frameSeq} apiId={apiId} -> localId={bestKey} IoU={bestIou:F2}"); } catch { }
                            }
                        }
                    }
                }

                // Update existing tracks with current detections
                var updatedIds = new HashSet<int>();
                if (current != null)
                {
                    foreach (var d in current)
                    {
                        // Non-tracked detections: push through directly (no smoothing possible)
                        if (d?.track_id == null)
                        {
                            d.hangover_frames = 0; // từ API trực tiếp (không có track)
                            result.Add(d);
                            continue;
                        }

                        var id = d.track_id.Value;
                        lock (_trackLock)
                        {
                            if (camTracks.TryGetValue(id, out var st))
                            {
                                // EMA smoothing towards current box
                                const double alphaCurr = 0.6; // trọng số cho bbox hiện tại
                                st.x1 = alphaCurr * d.x1 + (1 - alphaCurr) * st.x1;
                                st.y1 = alphaCurr * d.y1 + (1 - alphaCurr) * st.y1;
                                st.x2 = alphaCurr * d.x2 + (1 - alphaCurr) * st.x2;
                                st.y2 = alphaCurr * d.y2 + (1 - alphaCurr) * st.y2;
                                st.label = string.IsNullOrWhiteSpace(d.label) ? st.label : d.label;
                                st.score = d.score;
                                st.lastSeen = d.timestamp;
                                st.lastFrameSeq = d.frame_seq;
                                st.noUpdateFrames = 0; // fresh update this frame
                                // push coord signature (limit 3)
                                st.lastCoords.Enqueue((d.x1, d.y1, d.x2, d.y2));
                                while (st.lastCoords.Count > 3) st.lastCoords.Dequeue();
                            }
                            else
                            {
                                camTracks[id] = new TrackState
                                {
                                    x1 = d.x1,
                                    y1 = d.y1,
                                    x2 = d.x2,
                                    y2 = d.y2,
                                    label = d.label,
                                    score = d.score,
                                    lastSeen = d.timestamp,
                                    lastFrameSeq = d.frame_seq,
                                    noUpdateFrames = 0,
                                    lastCoords = new Queue<(double, double, double, double)>(new[] { (d.x1, d.y1, d.x2, d.y2) })
                                };
                            }
                            updatedIds.Add(id);
                        }
                    }
                }

                // For tracks not updated this frame, bump noUpdateFrames
                lock (_trackLock)
                {
                    foreach (var kv in camTracks)
                    {
                        if (!updatedIds.Contains(kv.Key))
                        {
                            kv.Value.noUpdateFrames = Math.Min(kv.Value.noUpdateFrames + 1, 1000000);
                        }
                    }
                }

                // Build output: include all live tracks within hangover window
                lock (_trackLock)
                {
                    var toRemove = new List<int>();
                    foreach (var kv in camTracks)
                    {
                        var st = kv.Value;
                        var ageMs = (now - st.lastSeen).TotalMilliseconds;
                        if (ageMs <= TRACK_MAX_AGE_MS)
                        {
                            // Keep drawing within hangover to avoid flicker due to occasional misses
                            if (ageMs <= TRACK_HANGOVER_MS)
                            {
                                // Ghost suppression: if no API update and bbox is stagnant for >3 frames, stop drawing
                                bool shouldDraw = true;
                                if (st.noUpdateFrames > 0)
                                {
                                    // Allow up to 3 frames of no-update
                                    if (st.noUpdateFrames > TRACK_HANGOVER_MAX_SAME_FRAMES)
                                    {
                                        shouldDraw = false;
                                    }
                                    else
                                    {
                                        // Optional: if last 3 coords are identical (stagnant), suppress at threshold
                                        if (st.lastCoords != null && st.lastCoords.Count >= 3)
                                        {
                                            var arr = st.lastCoords.ToArray();
                                            const double eps = 1e-3; // normalized tolerance
                                            bool same12 = Math.Abs(arr[^1].x1 - arr[^2].x1) < eps && Math.Abs(arr[^1].y1 - arr[^2].y1) < eps &&
                                                          Math.Abs(arr[^1].x2 - arr[^2].x2) < eps && Math.Abs(arr[^1].y2 - arr[^2].y2) < eps;
                                            bool same23 = Math.Abs(arr[^2].x1 - arr[^3].x1) < eps && Math.Abs(arr[^2].y1 - arr[^3].y1) < eps &&
                                                          Math.Abs(arr[^2].x2 - arr[^3].x2) < eps && Math.Abs(arr[^2].y2 - arr[^3].y2) < eps;
                                            if (st.noUpdateFrames >= TRACK_HANGOVER_MAX_SAME_FRAMES && same12 && same23)
                                            {
                                                shouldDraw = false;
                                            }
                                        }
                                    }
                                }

                                if (shouldDraw)
                                {
                                    if (DEBUG_VERBOSE_BBOX_LOG)
                                    {
                                        try { FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: Hangover track={kv.Key} lastSeen={st.lastSeen:HH:mm:ss.fff} ageMs={ageMs:F0} noUpd={st.noUpdateFrames}"); } catch { }
                                    }
                                    var outDet = new Detection
                                    {
                                        x1 = st.x1,
                                        y1 = st.y1,
                                        x2 = st.x2,
                                        y2 = st.y2,
                                        label = st.label,
                                        score = st.score,
                                        track_id = kv.Key,
                                        // use last seen time and frame to reflect true age
                                        timestamp = st.lastSeen,
                                        frame_seq = st.lastFrameSeq,
                                        hangover_frames = st.noUpdateFrames
                                    };
                                    result.Add(outDet);
                                }
                                else if (DEBUG_DETAILED_BBOX_TIMELINE)
                                {
                                    try { FileLogger.Log($"[HANGOVER-SUPPRESS] CAM {cameraIndex + 1} t={now:HH:mm:ss.fff} id={kv.Key} noUpd={st.noUpdateFrames} ageMs={ageMs:F0} lastSeq={st.lastFrameSeq} xy=({st.x1:F3},{st.y1:F3},{st.x2:F3},{st.y2:F3})"); } catch { }
                                }
                            }
                        }
                        else
                        {
                            toRemove.Add(kv.Key);
                        }
                    }
                    // Cleanup old tracks
                    foreach (var id in toRemove)
                        camTracks.Remove(id);
                }

            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "UpdateAndBuildTracks");
                // fallback to current if error
                if (current != null) result.AddRange(current);
            }
            return result;
        }

        // Gửi cảnh báo theo cấu hình (chỉ Telegram hiện tại) tới các ChatID đang IsActive=1
        private async Task SendAlarmToActiveRecipientsAsync(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return;
                // ByPass: nếu bật (1) thì bỏ qua không gửi
                if (ClassSystemConfig.Ins?.m_ClsCommon?.b_ByPassAlarm == 1) return;

                // Throttle: chỉ gửi tối đa 1 lần mỗi 10 giây
                var now = DateTime.Now;
                bool allowSend = false;
                lock (_alarmLock)
                {
                    if (_lastAlarmSentAt == DateTime.MinValue || (now - _lastAlarmSentAt).TotalMilliseconds >= ALARM_MIN_INTERVAL_MS)
                    {
                        _lastAlarmSentAt = now;
                        allowSend = true;
                    }
                }
                if (!allowSend)
                {
                    FileLogger.Log("SendAlarmToActiveRecipientsAsync: throttled (<=10s)");
                    return;
                }

                // 0: Telegram (theo form config)
                if (ClassSystemConfig.Ins?.m_ClsCommon?.m_iFormatSendMessage != 0) return;

                //// TẠM THỜI BỎ QUA TRUY XUẤT DB alarm_mes (bảng chưa tồn tại) — sẽ bật lại sau
                //FileLogger.Log("SendAlarmToActiveRecipientsAsync: Temporarily disabled DB access to alarm_mes");
                //return;

                var secrets = MessageSecretProvider.GetSecrets();
                if (!secrets.HasTelegramConfiguration)
                {
                    FileLogger.Log("SendAlarmToActiveRecipientsAsync: Telegram bot token is not configured");
                    return;
                }

                string botToken = secrets.TelegramBotToken;
                var recipients = new List<(string Name, string SDT, string ChatID)>();

                string connStr = ClassSystemConfig.Ins?.m_ClsCommon?.connectionString;
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    FileLogger.Log("SendAlarmToActiveRecipientsAsync: Missing DB connection string");
                    return;
                }

                using (var conn = new MySql.Data.MySqlClient.MySqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    string sql = "SELECT Name, SDT, ChatID FROM alarm_mes WHERE IsActive = 1 AND ChatID IS NOT NULL AND TRIM(ChatID) <> ''";
                    using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
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

                if (recipients.Count == 0) return;
                recipients = recipients
                    .GroupBy(r => (r.ChatID, r.Name, r.SDT))
                    .Select(g => g.First())
                    .ToList();

                using (var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(3.5) })
                {
                    foreach (var r in recipients)
                    {
                        try
                        {
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

                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status={(ok ? "SUCCESS" : "FAIL (HTTP)")}",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                        }
                        catch (TaskCanceledException tce)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status=TIMEOUT ({tce.Message})",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            FileLogger.LogException(tce, $"SendAlarmToActiveRecipientsAsync TIMEOUT -> ChatID={r.ChatID}");
                        }
                        catch (Exception exSend)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA,
                                $"TELEGRAM SEND | Name={r.Name} | ChatID={r.ChatID} | SDT={r.SDT} | Status=FAIL (EXCEPTION: {exSend.Message})",
                                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                            FileLogger.LogException(exSend, $"SendAlarmToActiveRecipientsAsync -> ChatID={r.ChatID}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "SendAlarmToActiveRecipientsAsync");
            }
        }

        // Convert detections (either normalized or pixel wrt square input) to normalized [0..1] on original frame
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

                    // Convert to square pixel coordinates first
                    double sx1 = squareNormalized ? d.x1 * squareSize : d.x1;
                    double sy1 = squareNormalized ? d.y1 * squareSize : d.y1;
                    double sx2 = squareNormalized ? d.x2 * squareSize : d.x2;
                    double sy2 = squareNormalized ? d.y2 * squareSize : d.y2;

                    // Map back to original frame pixel coordinates (undo letterbox + scale)
                    double ox1 = (sx1 - offX) / scale;
                    double oy1 = (sy1 - offY) / scale;
                    double ox2 = (sx2 - offX) / scale;
                    double oy2 = (sy2 - offY) / scale;

                    // Clamp
                    ox1 = Math.Max(0, Math.Min(frameW, ox1));
                    oy1 = Math.Max(0, Math.Min(frameH, oy1));
                    ox2 = Math.Max(0, Math.Min(frameW, ox2));
                    oy2 = Math.Max(0, Math.Min(frameH, oy2));

                    // Normalize to [0..1]
                    double nx1 = frameW > 0 ? ox1 / frameW : 0;
                    double ny1 = frameH > 0 ? oy1 / frameH : 0;
                    double nx2 = frameW > 0 ? ox2 / frameW : 0;
                    double ny2 = frameH > 0 ? oy2 / frameH : 0;

                    list.Add(new Detection
                    {
                        label = d.label,
                        score = d.score,
                        x1 = nx1,
                        y1 = ny1,
                        x2 = nx2,
                        y2 = ny2,
                        // preserve original timing and tracking info
                        timestamp = d.timestamp,
                        track_id = d.track_id,
                        frame_seq = d.frame_seq
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "NormalizeDetectionsToOriginalFrame");
                return new List<Detection>(input);
            }
        }

        private void TriggerPaintEvent(int cameraIndex, int detectionCount)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => TriggerPaintEvent(cameraIndex, detectionCount)));
                    return;
                }

                if (cameraIndex < _pictureboxes.Count && !_pictureboxes[cameraIndex].IsDisposed)
                {
                    var pictureBox = _pictureboxes[cameraIndex];
                    // Force refresh when we have detections to ensure overlay appears promptly
                    if (detectionCount > 0)
                    {
                        pictureBox.Refresh();
                    }
                    else
                    {
                        pictureBox.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"TriggerPaintEvent - Camera {cameraIndex + 1}");
            }
        }

        private void DrawDetectionBoxes(Graphics graphics, List<Detection> detections, int imageWidth, int imageHeight)
        {
            try
            {
                if (graphics == null || detections == null) return;

                foreach (var detection in detections)
                {
                    // Always draw; ignore score threshold

                    // Support both normalized [0,1] and absolute coordinates
                    // If any coordinate > 1, assume absolute pixels already
                    bool isNormalized = detection.x2 <= 1.5 && detection.y2 <= 1.5 && detection.x1 >= 0 && detection.y1 >= 0;

                    int x1 = (int)((isNormalized ? detection.x1 : detection.x1 / Math.Max(1, imageWidth)) * imageWidth);
                    int y1 = (int)((isNormalized ? detection.y1 : detection.y1 / Math.Max(1, imageHeight)) * imageHeight);
                    int x2 = (int)((isNormalized ? detection.x2 : detection.x2 / Math.Max(1, imageWidth)) * imageWidth);
                    int y2 = (int)((isNormalized ? detection.y2 : detection.y2 / Math.Max(1, imageHeight)) * imageHeight);

                    // Order and clamp to bounds
                    int left = Math.Max(0, Math.Min(imageWidth - 1, Math.Min(x1, x2)));
                    int top = Math.Max(0, Math.Min(imageHeight - 1, Math.Min(y1, y2)));
                    int right = Math.Max(0, Math.Min(imageWidth - 1, Math.Max(x1, x2)));
                    int bottom = Math.Max(0, Math.Min(imageHeight - 1, Math.Max(y1, y2)));
                    int w = Math.Max(1, right - left);
                    int h = Math.Max(1, bottom - top);

                    using (var pen = new Pen(Color.Red, 2))
                    {
                        Rectangle rect = new Rectangle(left, top, w, h);
                        graphics.DrawRectangle(pen, rect);
                    }

                    // Chỉ hiển thị nhãn khi API trả về action (label không rỗng)
                    if (!string.IsNullOrWhiteSpace(detection.label))
                    {
                        string labelText = $"{detection.label} ({detection.score:F2})";
                        if (detection.track_id.HasValue)
                        {
                            labelText = $"{detection.label} (ID: {detection.track_id.Value}) ({detection.score:F2})";
                        }
                        using (var font = new Font("Arial", 11, FontStyle.Bold))
                        {
                            using (var textBrush = new SolidBrush(Color.White))
                            {
                                graphics.DrawString(labelText, font, textBrush, new PointF(left + 3, Math.Max(0, top - 18)));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "DrawDetectionBoxes");
            }
        }

        // Save annotated image to folder structure yyyy/MM/dd/image_#.jpg
        private void SaveDetectionImage(int cameraIndex, Bitmap frame, List<Detection> detections)
        {
            try
            {
                if (frame == null || detections == null || detections.Count == 0) return;

                using (var annotated = (Bitmap)frame.Clone())
                using (var g = Graphics.FromImage(annotated))
                {
                    DrawDetectionBoxes(g, detections, annotated.Width, annotated.Height);

                    string baseRoot = null;
                    try { baseRoot = ClassSystemConfig.Ins?.m_ClsCommon?.m_CommonPath; } catch { baseRoot = null; }
                    if (string.IsNullOrWhiteSpace(baseRoot))
                    {
                        baseRoot = Environment.CurrentDirectory;
                    }
                    string imagesRoot = Path.Combine(baseRoot, "Images");

                    var now = DateTime.Now;
                    string saveDir = Path.Combine(
                        imagesRoot,
                        now.ToString("yyyy"),        // 2025
                        now.ToString("MM_yyyy"),     // 09_2025
                        now.ToString("dd_MM_yyyy"),  // 03_09_2025
                        "graphic");
                    Directory.CreateDirectory(saveDir);

                    int nextIdx = GetNextImageIndex(saveDir);
                    string fileName = $"cam_{cameraIndex + 1}_{now:HH_mm_ss_fff}_{nextIdx}.jpg";
                    string filePath = Path.Combine(saveDir, fileName);

                    annotated.Save(filePath, ImageFormat.Jpeg);
                    FileLogger.Log($"Saved detection image (cam {cameraIndex + 1}): {filePath}");

                    // Also log to database for tracking
                    try
                    {
                        var labels = detections
                            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.label))
                            .Select(d => d.label?.Trim() ?? "")
                            .ToList();

                        // Map labels to only FIRE or SMOKE
                        bool hasFire = labels.Any(l => l.Equals("fire", StringComparison.OrdinalIgnoreCase) || l.Equals("flame", StringComparison.OrdinalIgnoreCase) || l.Contains("fire", StringComparison.OrdinalIgnoreCase) || l.Contains("flame", StringComparison.OrdinalIgnoreCase));
                        bool hasSmoke = labels.Any(l => l.Equals("smoke", StringComparison.OrdinalIgnoreCase) || l.Contains("smoke", StringComparison.OrdinalIgnoreCase));
                        string eventText = hasFire ? "FIRE" : (hasSmoke ? "SMOKE" : "FIRE");

                        AddCameraLogData(cameraIndex + 1, DateTime.Now, eventText, filePath);

                        // Tự động refresh bảng log sau khi có detection mới
                        try { UpdateCameraLogInvoke(this); } catch { }
                    }
                    catch (Exception exAddLog)
                    {
                        FileLogger.LogException(exAddLog, "SaveDetectionImage -> AddCameraLogData");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "SaveDetectionImage");
            }
        }

        private void AddCameraLogData(int cameraNumber, DateTime time, string eventText, string imagePath)
        {
            try
            {
                string connStr = null;
                try { connStr = ClassSystemConfig.Ins?.m_ClsCommon?.connectionString; } catch { connStr = null; }
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    FileLogger.Log("AddCameraLogData: Missing connection string");
                    return;
                }

                using (var conn = new MySql.Data.MySqlClient.MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "INSERT INTO camera_log (`Camera`, `Time`, `Event`, `image_Path`) VALUES (@cam, @time, @event, @path)";
                    using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
                    {
                        string camName = $"CAM{cameraNumber}";
                        cmd.Parameters.AddWithValue("@cam", camName);
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.Parameters.AddWithValue("@event", string.IsNullOrWhiteSpace(eventText) ? "FIRE" : eventText);
                        cmd.Parameters.AddWithValue("@path", imagePath ?? string.Empty);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "AddCameraLogData");
            }
        }

        private int GetNextImageIndex(string directory)
        {
            try
            {
                if (!Directory.Exists(directory)) return 1;
                int maxIdx = 0;

                foreach (var file in Directory.GetFiles(directory, "*.jpg"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);

                    // Pattern 1: image_<idx>.jpg
                    if (name.StartsWith("image_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(name.Substring(6), out int idx1))
                            if (idx1 > maxIdx) maxIdx = idx1;
                        continue;
                    }

                    // Pattern 2: cam_<camIdx>_<HH_mm_ss_fff>_<idx>.jpg
                    if (name.StartsWith("cam_", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = name.Split('_');
                        if (parts.Length >= 5)
                        {
                            // last part should be index
                            if (int.TryParse(parts[parts.Length - 1], out int idx2))
                                if (idx2 > maxIdx) maxIdx = idx2;
                        }
                        continue;
                    }
                }

                return maxIdx + 1;
            }
            catch
            {
                return 1;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
                MaxConnectionsPerServer = 16,
                AutomaticDecompression = DecompressionMethods.All,
                EnableMultipleHttp2Connections = false
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(3.5)
            };
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            return client;
        }

        private static Bitmap ResizeToMaxWidth(Bitmap src, int maxWidth)
        {
            if (src == null) return null;
            if (maxWidth <= 0 || src.Width <= maxWidth) return (Bitmap)src.Clone();

            double scale = (double)maxWidth / src.Width;
            int newW = maxWidth;
            int newH = Math.Max(1, (int)Math.Round(src.Height * scale));

            var dst = new Bitmap(newW, newH);
            using (var g = Graphics.FromImage(dst))
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, new Rectangle(0, 0, newW, newH));
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

        // TestDetectionOverlay removed per request

        #endregion

        #region Core Implementation Methods

        private void InitializeUI()
        {
            try
            {
                LoadDeviceConfig(false);

                ClassSystemConfig.Ins.m_CameraList.InitializeUI(this);
                ClassSystemConfig.Ins.m_FrmParamCamera.Innit(this);
                FileLogger.Log("UI initialized successfully");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "InitializeUI");
            }
        }

        private void LoadCameraList()
        {
            try
            {
                Console.WriteLine("?? Loading camera list from database...");

                ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Clear();
                ClassSystemConfig.Ins.m_ClsFunc.GetRtspUrls(connection, ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam);

                if (ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count > 0)
                {
                    Console.WriteLine($"?? Found {ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count} RTSP URLs in database");
                    UpdateLayoutForCameraCount();
                }
                else
                {
                    Console.WriteLine("?? No RTSP URLs found in database, using default configuration");
                    AddDefaultRtspUrls();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error in LoadCameraList: {ex.Message}");
                FileLogger.LogException(ex, "LoadCameraList");
                AddDefaultRtspUrls();
            }
        }

        private void AddDefaultRtspUrls()
        {
            try
            {
                ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Clear();
                ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.AddRange(new List<string>
                {
                    "rtsp://admin:infiniq2025@10.29.98.55:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.56:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.57:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.58:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.59:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.60:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.61:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.62:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.63:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.64:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.53:554/cam/realmonitor?channel=1&subtype=1",
                    "rtsp://admin:infiniq2025@10.29.98.54:554/cam/realmonitor?channel=1&subtype=1"
                });

                Console.WriteLine($"? Added {ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count} default RTSP URLs");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "AddDefaultRtspUrls");
            }
        }

        private void UpdateLayoutForCameraCount()
        {
            try
            {
                // Use the active camera count (override-aware)
                int cameraCount = ActiveCameraCount;

                if (cameraCount == 1)
                {
                    Row = 1; Col = 1;
                }
                else if (cameraCount <= 4)
                {
                    Row = 2; Col = 2;
                }
                else if (cameraCount <= 6)
                {
                    Row = 2; Col = 3;
                }
                else if (cameraCount <= 9)
                {
                    Row = 3; Col = 3;
                }
                else if (cameraCount <= 12)
                {
                    Row = 3; Col = 4;
                }
                else if (cameraCount <= 16)
                {
                    Row = 4; Col = 4;
                }
                else
                {
                    int sqrt = (int)Math.Ceiling(Math.Sqrt(cameraCount));
                    Row = sqrt;
                    Col = sqrt;
                }

                Console.WriteLine($"?? Updated layout for {cameraCount} cameras: {Row}x{Col} grid");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "UpdateLayoutForCameraCount");
            }
        }

        private void ApplyLogPanelCollapsed(bool collapse)
        {
            try
            {
                if (tableLayoutPanel2 == null || panelMain == null || panelLog == null) return;
                if (collapse == _isLogCollapsed) return;

                tableLayoutPanel2.SuspendLayout();
                if (collapse)
                {
                    panelLog.Visible = false;
                    if (tableLayoutPanel2.ColumnStyles.Count >= 2)
                    {
                        tableLayoutPanel2.ColumnStyles[0].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[0].Width = 0f;
                        tableLayoutPanel2.ColumnStyles[1].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[1].Width = 100f;
                    }
                    try
                    {
                        tableLayoutPanel2.SetColumn(panelMain, 0);
                        tableLayoutPanel2.SetColumnSpan(panelMain, 2);
                    }
                    catch { }
                    _isLogCollapsed = true;
                }
                else
                {
                    try
                    {
                        tableLayoutPanel2.SetColumnSpan(panelMain, 1);
                        tableLayoutPanel2.SetColumn(panelMain, 1);
                    }
                    catch { }
                    if (tableLayoutPanel2.ColumnStyles.Count >= 2)
                    {
                        tableLayoutPanel2.ColumnStyles[0].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[0].Width = _origCol0Width;
                        tableLayoutPanel2.ColumnStyles[1].SizeType = SizeType.Percent;
                        tableLayoutPanel2.ColumnStyles[1].Width = _origCol1Width;
                    }
                    panelLog.Visible = true;
                    _isLogCollapsed = false;
                }
                tableLayoutPanel2.ResumeLayout(true);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(ApplyLogPanelCollapsed));
            }
        }

        public void LayoutCameraSpreadView()
        {
            try
            {
                if (panelMain.InvokeRequired)
                {
                    panelMain.Invoke(new Action(() => LayoutCameraSpreadView()));
                    return;
                }

                FileLogger.Log("Setting up dynamic camera layout...");

                // Adjust layout and log panel for active camera count
                UpdateLayoutForCameraCount();
                ApplyLogPanelCollapsed(ActiveCameraCount == 1);

                panelMain.Controls.Clear();
                _pictureboxes.Clear();

                tableLayoutPanelCamera = new TableLayoutPanel();
                tableLayoutPanelCamera.Dock = DockStyle.Fill;
                tableLayoutPanelCamera.ColumnCount = 1;
                tableLayoutPanelCamera.RowCount = Row;
                tableLayoutPanelCamera.ColumnStyles.Clear();
                tableLayoutPanelCamera.RowStyles.Clear();

                panelMain.Controls.Add(tableLayoutPanelCamera);

                tableLayoutPanelDevice = new TableLayoutPanel[Row];

                for (int i = 0; i < Row; i++)
                {
                    tableLayoutPanelCamera.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / Row));

                    tableLayoutPanelDevice[i] = new TableLayoutPanel();
                    tableLayoutPanelCamera.Controls.Add(tableLayoutPanelDevice[i], 0, i);
                    tableLayoutPanelDevice[i].Dock = DockStyle.Fill;
                    tableLayoutPanelDevice[i].ColumnCount = Col;
                    tableLayoutPanelDevice[i].RowCount = 1;

                    for (int j = 0; j < Col; j++)
                    {
                        tableLayoutPanelDevice[i].ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Col));
                    }
                    tableLayoutPanelDevice[i].RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                }

                int indexCam = 0;
                for (int row = 0; row < Row; row++)
                {
                    for (int col = 0; col < Col; col++)
                    {
                        if (indexCam < ActiveCameraCount)
                        {
                            var pictureBox = new PictureBox();
                            pictureBox.Name = $"pictureBox{indexCam + 1}";
                            pictureBox.Dock = DockStyle.Fill;
                            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                            pictureBox.BackColor = Color.Black;
                            pictureBox.BorderStyle = BorderStyle.FixedSingle;

                            // Add context menu for saving image
                            var cms = new ContextMenuStrip();
                            var saveItem = new ToolStripMenuItem("Save Image");
                            int capturedIndex = indexCam;
                            saveItem.Click += (s, e) => SaveImageFromPictureBox(pictureBox, capturedIndex);
                            cms.Items.Add(saveItem);
                            pictureBox.ContextMenuStrip = cms;

                            var label = new Label();
                            label.Name = "OverlayLabel";
                            label.Text = $"CAM {indexCam + 1}";
                            label.ForeColor = Color.White;
                            label.BackColor = Color.Transparent;
                            label.Font = new Font("Arial", 9, FontStyle.Bold);
                            label.TextAlign = ContentAlignment.MiddleCenter;
                            label.Dock = DockStyle.Fill;
                            label.Anchor = AnchorStyles.None;
                            label.Visible = false; // hidden until Start and signal check

                            pictureBox.Controls.Add(label);

                            int cameraIndex = indexCam;
                            pictureBox.DoubleClick += (sender, e) => ToggleFullscreen(cameraIndex);
                            pictureBox.Cursor = Cursors.Hand;

                            pictureBox.Click += (sender, e) =>
                            {
                                if (!_isFullscreen)
                                {
                                    Console.WriteLine($"Camera {cameraIndex + 1} selected");
                                }
                            };

                            pictureBox.MouseEnter += (sender, e) =>
                            {
                                if (!_isFullscreen || _fullscreenCameraIndex != cameraIndex)
                                {
                                    pictureBox.BorderStyle = BorderStyle.Fixed3D;
                                }
                            };

                            pictureBox.MouseLeave += (sender, e) =>
                            {
                                if (!_isFullscreen || _fullscreenCameraIndex != cameraIndex)
                                {
                                    pictureBox.BorderStyle = BorderStyle.FixedSingle;
                                }
                            };

                            pictureBox.Paint += (sender, e) => OnPictureBoxPaint(sender, e, cameraIndex);

                            tableLayoutPanelDevice[row].Controls.Add(pictureBox, col, 0);
                            _pictureboxes.Add(pictureBox);

                            indexCam++;
                        }
                    }
                }

                // Ensure detection slots match number of picture boxes
                EnsureCameraDetectionsSize(_pictureboxes.Count);

                FileLogger.Log($"? Created dynamic layout with {_pictureboxes.Count} camera PictureBoxes");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "LayoutCameraSpreadView");
            }
        }

        private void SaveImageFromPictureBox(PictureBox pb, int cameraIndex)
        {
            try
            {
                Bitmap? frame = null;
                lock (_frameStoreLock)
                {
                    if (_latestFrames.TryGetValue(cameraIndex, out var latest) && latest != null)
                    {
                        frame = (Bitmap)latest.Clone(); // clone raw frame (no overlay)
                    }
                    else if (pb?.Image != null)
                    {
                        frame = new Bitmap(pb.Image); // fallback: clone current picture
                    }
                }

                if (frame == null)
                {
                    MessageBox.Show("No image to save.", "Save Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (frame)
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Save Image";
                    sfd.FileName = $"Camera_{cameraIndex + 1}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|Bitmap Image|*.bmp";
                    sfd.FilterIndex = 1;

                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        var ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                        ImageFormat format = ImageFormat.Png;
                        if (ext == ".jpg" || ext == ".jpeg") format = ImageFormat.Jpeg;
                        else if (ext == ".bmp") format = ImageFormat.Bmp;

                        frame.Save(sfd.FileName, format);
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"SaveImageFromPictureBox - Camera {cameraIndex + 1}");
                MessageBox.Show($"Error saving image: {ex.Message}", "Save Image Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnPictureBoxPaint(object sender, PaintEventArgs e, int cameraIndex)
        {
            try
            {
                EnsureCameraDetectionsSize(cameraIndex + 1);
                if (_isShuttingDown || cameraIndex >= _cameraDetections.Count) return;

                var pictureBox = sender as PictureBox;
                if (pictureBox == null) return;

                // PictureBox will draw its Image by itself. We only draw overlay.

                if (DEBUG_CLEAR_PICTUREBOX_BEFORE_DRAW)
                {
                    try { e.Graphics.Clear(Color.Black); } catch { }
                }

                // Always draw region overlay (from DB) if available
                DrawRegionOverlayIfAvailable(e.Graphics, pictureBox, cameraIndex);

                List<Detection> detections = null;
                lock (_cameraDetections)
                {
                    if (cameraIndex < _cameraDetections.Count && _cameraDetections[cameraIndex].Count > 0)
                    {
                        detections = new List<Detection>(_cameraDetections[cameraIndex]);
                    }
                }

                if (detections?.Count > 0)
                {
                    if (DEBUG_VERBOSE_BBOX_LOG)
                    {
                        try
                        {
                            string ts = string.Join(", ", detections.Select(d => d.timestamp.ToString("HH:mm:ss.fff")));
                            FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: Draw start count={detections.Count} ts=[{ts}]");
                        }
                        catch { }
                    }
                    DrawDetectionsWithRegion(e.Graphics, detections, pictureBox, cameraIndex);
                }

                // Draw blinking alert border if active
                try
                {
                    if (_alertsByCam.TryGetValue(cameraIndex, out var alert) && alert.Active && alert.BlinkOn)
                    {
                        using (var pen = new Pen(Color.Red, 6))
                        {
                            pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                            e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, pictureBox.Width - 1, pictureBox.Height - 1));
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"OnPictureBoxPaint - Camera {cameraIndex}");
            }
        }

        private void ActivateCameraAlert(int cameraIndex, string label)
        {
            try
            {
                if (cameraIndex < 0 || cameraIndex >= ActiveCameraCount) return;
                if (!_alertsByCam.TryGetValue(cameraIndex, out var st))
                {
                    st = new CameraAlertState();
                    _alertsByCam[cameraIndex] = st;
                }
                st.Active = true;
                st.Label = label ?? string.Empty;
                st.BlinkOn = true;
                st.LastBlinkAt = DateTime.Now;
                if (st.LastAlarmAt == default) st.LastAlarmAt = DateTime.MinValue;

                // Show confirm popup at most once per 10s
                var now = DateTime.Now;
                if (st.LastPopupAt == default || (now - st.LastPopupAt).TotalMilliseconds >= ALARM_MIN_INTERVAL_MS)
                {
                    ShowConfirmPopup(cameraIndex, label);
                    st.LastPopupAt = now;
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(ActivateCameraAlert));
            }
        }

        private void ConfirmCameraAlert(int cameraIndex)
        {
            try
            {
                if (cameraIndex < 0) return;
                if (_alertsByCam.TryGetValue(cameraIndex, out var st))
                {
                    st.Active = false;
                    st.BlinkOn = false;
                }
                if (_pictureboxes != null && cameraIndex < _pictureboxes.Count)
                {
                    try { _pictureboxes[cameraIndex]?.Invalidate(); } catch { }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(ConfirmCameraAlert));
            }
        }

        private void ShowConfirmPopup(int cameraIndex, string label)
        {
            try
            {
                if (_confirmDialog == null || _confirmDialog.IsDisposed)
                {
                    _confirmDialog = new DKVN.FormConfirmVision();
                    _confirmDialog.OnConfirm = (idx) => { ConfirmCameraAlert(idx); };
                }
                var msg = string.IsNullOrWhiteSpace(label) ? "Vision detection Warning. Please confirm." : $"{label} detected. Please confirm.";
                _confirmDialog.SetAlarm(msg, cameraIndex);
                // Center near the camera picturebox if possible
                try
                {
                    if (cameraIndex >= 0 && cameraIndex < _pictureboxes.Count)
                    {
                        var pb = _pictureboxes[cameraIndex];
                        if (pb != null && pb.FindForm() != null)
                        {
                            var screenPos = pb.PointToScreen(new Point(pb.Width / 2, pb.Height / 2));
                            _confirmDialog.StartPosition = FormStartPosition.Manual;
                            _confirmDialog.Location = new Point(screenPos.X - _confirmDialog.Width / 2, screenPos.Y - _confirmDialog.Height / 2);
                        }
                    }
                }
                catch { }
                try { _confirmDialog.Show(); _confirmDialog.BringToFront(); } catch { }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(ShowConfirmPopup));
            }
        }

        private void DrawRegionOverlayIfAvailable(Graphics g, PictureBox pb, int cameraIndex)
        {
            try
            {
                if (g == null || pb == null) return;

                // Load region for this camera on first use
                EnsureRegionLoaded(cameraIndex);

                Point[] pts = GetMappedRegionPoints(cameraIndex, pb);
                if (pts == null || pts.Length < 4) return;

                using (var pen = new Pen(Color.Yellow, 2))
                using (var brush = new SolidBrush(Color.Yellow))
                {
                    // lines 0-1, 1-2, 2-3, 3-0
                    g.DrawLine(pen, pts[0], pts[1]);
                    g.DrawLine(pen, pts[1], pts[2]);
                    g.DrawLine(pen, pts[2], pts[3]);
                    g.DrawLine(pen, pts[3], pts[0]);

                    // small points
                    const int r = 3;
                    for (int i = 0; i < 4; i++)
                    {
                        g.FillEllipse(brush, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"DrawRegionOverlayIfAvailable cam={cameraIndex + 1}");
            }
        }

        private PointF[] GetRegionPointsNormalized(int cameraIndex, int frameW, int frameH)
        {
            try
            {
                RegionData rd = null;
                lock (_regionLock)
                {
                    _regionDataByCam.TryGetValue(cameraIndex, out rd);
                }
                if (rd == null || rd.Points == null || rd.Points.Count < 3) return null;
                if (rd.IsNormalized)
                {
                    return rd.Points.ToArray();
                }
                // Convert absolute to normalized by frame size
                if (frameW <= 0 || frameH <= 0) return null;
                var pts = new PointF[rd.Points.Count];
                for (int i = 0; i < rd.Points.Count; i++)
                {
                    pts[i] = new PointF(
                        (float)(rd.Points[i].X / Math.Max(1f, frameW)),
                        (float)(rd.Points[i].Y / Math.Max(1f, frameH))
                    );
                }
                return pts;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPointInPolygonF(PointF point, PointF[] polygon)
        {
            try
            {
                if (polygon == null || polygon.Length < 3) return false;
                bool inside = false;
                int j = polygon.Length - 1;
                for (int i = 0; i < polygon.Length; i++)
                {
                    var pi = polygon[i];
                    var pj = polygon[j];
                    bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                     (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (float)(pj.Y - pi.Y) + pi.X);
                    if (intersect) inside = !inside;
                    j = i;
                }
                return inside;
            }
            catch { return false; }
        }

        private Point[] GetMappedRegionPoints(int cameraIndex, PictureBox pb)
        {
            try
            {
                RegionData rd = null;
                lock (_regionLock)
                {
                    _regionDataByCam.TryGetValue(cameraIndex, out rd);
                }
                if (rd == null || rd.Points == null || rd.Points.Count < 4 || pb == null) return null;

                var pts = new Point[4];
                if (rd.IsNormalized)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        pts[i] = new Point(
                            (int)Math.Round(rd.Points[i].X * pb.Width),
                            (int)Math.Round(rd.Points[i].Y * pb.Height)
                        );
                    }
                }
                else
                {
                    if (pb.Image == null) return null;
                    float sx = pb.Image.Width > 0 ? (float)pb.Width / pb.Image.Width : 1f;
                    float sy = pb.Image.Height > 0 ? (float)pb.Height / pb.Image.Height : 1f;
                    for (int i = 0; i < 4; i++)
                    {
                        pts[i] = new Point(
                            (int)Math.Round(rd.Points[i].X * sx),
                            (int)Math.Round(rd.Points[i].Y * sy)
                        );
                    }
                }
                return pts;
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"GetMappedRegionPoints cam={cameraIndex + 1}");
                return null;
            }
        }

        private static bool IsPointInPolygon(PointF point, Point[] polygon)
        {
            try
            {
                if (polygon == null || polygon.Length < 3) return false;
                bool inside = false;
                int j = polygon.Length - 1;
                for (int i = 0; i < polygon.Length; i++)
                {
                    var pi = polygon[i];
                    var pj = polygon[j];
                    bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                     (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (float)(pj.Y - pi.Y) + pi.X);
                    if (intersect) inside = !inside;
                    j = i;
                }
                return inside;
            }
            catch { return false; }
        }

        private void DrawDetectionsWithRegion(Graphics graphics, List<Detection> detections, PictureBox pictureBox, int cameraIndex)
        {
            try
            {
                if (graphics == null || detections == null || pictureBox == null) return;
                int imageWidth = pictureBox.Width;
                int imageHeight = pictureBox.Height;
                Point[] regionPts = GetMappedRegionPoints(cameraIndex, pictureBox);

                var now = DateTime.Now;
                long latestSeq = 0;
                try
                {
                    lock (_frameStoreLock)
                    {
                        _frameSeqByCam.TryGetValue(cameraIndex, out latestSeq);
                    }
                }
                catch { }
                // Vẽ theo thứ tự ổn định theo track_id
                detections = detections
                    .OrderBy(d => d?.track_id ?? int.MaxValue)
                    .ThenBy(d => ((d?.x1 ?? 0) + (d?.x2 ?? 0)) * 0.5)
                    .ToList();

                int drawn = 0, skipTtl = 0, skipLag = 0, skipWeak = 0, insideCount = 0;
                foreach (var detection in detections)
                {
                    // Bỏ qua bbox quá cũ để tránh vẽ bóng ma
                    if ((now - detection.timestamp).TotalMilliseconds > DRAW_TTL_MS)
                    {
                        if (DEBUG_DETAILED_BBOX_TIMELINE)
                        {
                            try { FileLogger.Log($"[DRAW-SKIP] CAM {cameraIndex + 1} t={now:HH:mm:ss.fff} reason=TTL ageMs={(now - detection.timestamp).TotalMilliseconds:F0} id={(detection.track_id?.ToString() ?? "-")} detSeq={detection.frame_seq} hangover={detection.hangover_frames}"); } catch { }
                        }
                        skipTtl++; continue;
                    }
                    // Bỏ qua bbox lệch quá nhiều frame so với hình hiện đang hiển thị
                    if (detection.frame_seq > 0 && latestSeq > 0)
                    {
                        long lag = latestSeq - detection.frame_seq;
                        if (lag > DETECTION_MAX_FRAME_LAG)
                        {
                            if (DEBUG_VERBOSE_BBOX_LOG || DEBUG_DETAILED_BBOX_TIMELINE)
                            {
                                try { FileLogger.Log($"[DRAW-SKIP] CAM {cameraIndex + 1} t={now:HH:mm:ss.fff} reason=LAG lag={lag} latestSeq={latestSeq} detSeq={detection.frame_seq} id={(detection.track_id?.ToString() ?? "-")} hangover={detection.hangover_frames}"); } catch { }
                            }
                            skipLag++; continue;
                        }
                    }

                    bool isNormalized = detection.x2 <= 1.5 && detection.y2 <= 1.5 && detection.x1 >= 0 && detection.y1 >= 0;

                    int x1 = (int)((isNormalized ? detection.x1 : detection.x1 / Math.Max(1, imageWidth)) * imageWidth);
                    int y1 = (int)((isNormalized ? detection.y1 : detection.y1 / Math.Max(1, imageHeight)) * imageHeight);
                    int x2 = (int)((isNormalized ? detection.x2 : detection.x2 / Math.Max(1, imageWidth)) * imageWidth);
                    int y2 = (int)((isNormalized ? detection.y2 : detection.y2 / Math.Max(1, imageHeight)) * imageHeight);

                    int left = Math.Max(0, Math.Min(imageWidth - 1, Math.Min(x1, x2)));
                    int top = Math.Max(0, Math.Min(imageHeight - 1, Math.Min(y1, y2)));
                    int right = Math.Max(0, Math.Min(imageWidth - 1, Math.Max(x1, x2)));
                    int bottom = Math.Max(0, Math.Min(imageHeight - 1, Math.Max(y1, y2)));
                    int w = Math.Max(1, right - left);
                    int h = Math.Max(1, bottom - top);

                    bool insideRegion = false;
                    if (regionPts != null && regionPts.Length >= 3)
                    {
                        float cx = left + w / 2f;
                        float cy = top + h / 2f;
                        insideRegion = IsPointInPolygon(new PointF(cx, cy), regionPts);
                    }

                    // Tuỳ chọn: Bỏ qua bbox yếu nếu bật cờ, mặc định không bỏ để tránh hụt phát hiện
                    if (FILTER_WEAK_BOXES && string.IsNullOrWhiteSpace(detection.label) && (detection.score < MIN_DRAW_SCORE))
                    {
                        if (DEBUG_VERBOSE_BBOX_LOG)
                        {
                            try { FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: skip weak box score={detection.score:F2}"); } catch { }
                        }
                        skipWeak++; continue;
                    }

                    using (var pen = new Pen(insideRegion ? Color.Red : Color.Lime, 2))
                    {
                        Rectangle rect = new Rectangle(left, top, w, h);
                        graphics.DrawRectangle(pen, rect);
                    }
                    drawn++;
                    if (DEBUG_DETAILED_BBOX_TIMELINE)
                    {
                        string src = detection.hangover_frames > 0 ? "HANGOVER" : "API";
                        try { FileLogger.Log($"[DRAW] CAM {cameraIndex + 1} t={now:HH:mm:ss.fff} latestSeq={latestSeq} detSeq={detection.frame_seq} src={src} hangover={detection.hangover_frames} id={(detection.track_id?.ToString() ?? "-")} xy=({detection.x1:F3},{detection.y1:F3},{detection.x2:F3},{detection.y2:F3}) inside={insideRegion}"); } catch { }
                    }
                    if (insideRegion) insideCount++;

                    if (insideRegion && !string.IsNullOrWhiteSpace(detection.label))
                    {
                        string labelText = detection.label;
                        using (var font = new Font("Arial", 11, FontStyle.Bold))
                        using (var textBrush = new SolidBrush(Color.Red))
                        using (var backBrush = new SolidBrush(Color.FromArgb(80, Color.White)))
                        {
                            var size = graphics.MeasureString(labelText, font);
                            var labelRect = new RectangleF(left, Math.Max(0, top - size.Height - 4), size.Width + 6, size.Height + 4);
                            graphics.FillRectangle(backBrush, labelRect);
                            graphics.DrawString(labelText, font, textBrush, labelRect.Location + new SizeF(3, 2));
                        }
                    }
                }
                if (DEBUG_VERBOSE_BBOX_LOG)
                {
                    try { FileLogger.Log($"[DEBUG] CAM {cameraIndex + 1}: paint summary drawn={drawn} skip_ttl={skipTtl} skip_lag={skipLag} skip_weak={skipWeak} inside={insideCount} total={detections.Count} latestSeq={latestSeq}"); } catch { }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "DrawDetectionsWithRegion");
            }
        }

        private static Color GetTrackColor(int? trackId, bool insideRegion)
        {
            try
            {
                if (!trackId.HasValue)
                    return insideRegion ? Color.Red : Color.Lime;
                int id = trackId.Value;
                // Generate stable color from id
                unchecked
                {
                    int r = (id * 73) % 256;
                    int g = (id * 151 + 50) % 256;
                    int b = (id * 199 + 100) % 256;
                    var baseColor = Color.FromArgb(r, g, b);
                    return insideRegion ? baseColor : ControlPaint.Light(baseColor);
                }
            }
            catch { return insideRegion ? Color.Red : Color.Lime; }
        }

        private static List<Detection> DeduplicateOverlapping(List<Detection> list, double iouThreshold)
        {
            try
            {
                if (list == null || list.Count <= 1) return list;
                var result = new List<Detection>();
                foreach (var d in list)
                {
                    bool dup = result.Any(e => IoU(d, e) >= iouThreshold && (d.track_id == e.track_id || !d.track_id.HasValue || !e.track_id.HasValue));
                    if (!dup) result.Add(d);
                }
                return result;
            }
            catch { return list; }
        }

        private static double IoU(Detection a, Detection b)
        {
            try
            {
                if (a == null || b == null) return 0;
                double ax1 = Math.Min(a.x1, a.x2), ay1 = Math.Min(a.y1, a.y2);
                double ax2 = Math.Max(a.x1, a.x2), ay2 = Math.Max(a.y1, a.y2);
                double bx1 = Math.Min(b.x1, b.x2), by1 = Math.Min(b.y1, b.y2);
                double bx2 = Math.Max(b.x1, b.x2), by2 = Math.Max(b.y1, b.y2);
                double ix1 = Math.Max(ax1, bx1), iy1 = Math.Max(ay1, by1);
                double ix2 = Math.Min(ax2, bx2), iy2 = Math.Min(ay2, by2);
                double iw = Math.Max(0, ix2 - ix1), ih = Math.Max(0, iy2 - iy1);
                double inter = iw * ih;
                double areaA = Math.Max(0, ax2 - ax1) * Math.Max(0, ay2 - ay1);
                double areaB = Math.Max(0, bx2 - bx1) * Math.Max(0, by2 - by1);
                double uni = Math.Max(1e-9, areaA + areaB - inter);
                return inter / uni;
            }
            catch { return 0; }
        }

        private void EnsureRegionLoaded(int cameraIndex)
        {
            try
            {
                lock (_regionLock)
                {
                    if (_regionDataByCam.ContainsKey(cameraIndex)) return;
                }

                // STT is 1-based
                int stt = cameraIndex + 1;
                var points = new List<PointF>(4);
                bool isNormalized = true;

                try
                {
                    using (var conn = new MySql.Data.MySqlClient.MySqlConnection(ClassSystemConfig.Ins?.m_ClsCommon?.connectionString))
                    {
                        conn.Open();
                        string sql = "SELECT x1, y1, x2, y2, x3, y3, x4, y4 FROM camera_list WHERE STT = @STT LIMIT 1";
                        using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
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

                                    // Heuristic: treat as normalized if all are within [0,1.5]
                                    isNormalized = vals.All(v => v >= 0 && v <= 1.5);
                                    points.Add(new PointF((float)vals[0], (float)vals[1]));
                                    points.Add(new PointF((float)vals[2], (float)vals[3]));
                                    points.Add(new PointF((float)vals[4], (float)vals[5]));
                                    points.Add(new PointF((float)vals[6], (float)vals[7]));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.LogException(ex, $"EnsureRegionLoaded DB STT={stt}");
                }

                lock (_regionLock)
                {
                    _regionDataByCam[cameraIndex] = new RegionData
                    {
                        Points = points,
                        IsNormalized = isNormalized
                    };
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"EnsureRegionLoaded cam={cameraIndex + 1}");
            }
        }

        private void UpdateNoSignalOverlay(int cameraIndex)
        {
            if (cameraIndex < 0 || cameraIndex >= _pictureboxes.Count) return;
            var pb = _pictureboxes[cameraIndex];
            if (pb == null || pb.IsDisposed) return;

            DateTime lastAt;
            lock (_frameStoreLock)
            {
                _lastFrameAt.TryGetValue(cameraIndex, out lastAt);
            }

            var now = DateTime.Now;
            bool stale = (lastAt == default) || (now - lastAt).TotalMilliseconds > NO_SIGNAL_TIMEOUT_MS;

            if (pb.Controls.Count > 0)
            {
                if (pb.Controls[0] is Label overlay)
                {
                    if (stale)
                    {
                        overlay.Text = $"CAM {cameraIndex + 1}\nNo Signal (reconnecting...)";
                        overlay.Visible = true;
                        TryRestartStalledCamera(cameraIndex, now, lastAt);
                    }
                    else
                    {
                        overlay.Visible = false;
                    }
                }
            }
        }

        private void TryRestartStalledCamera(int cameraIndex, DateTime now, DateTime lastAt)
        {
            try
            {
                if (cameraIndex < 0 || cameraIndex >= _supervisors.Count) return;
                // If we've seen a frame within RESTART_STALL_MS, don't restart
                if (lastAt != default && (now - lastAt).TotalMilliseconds <= RESTART_STALL_MS) return;

                // Cooldown between restarts
                if (_lastRestartAt.TryGetValue(cameraIndex, out var lastRestart))
                {
                    if ((now - lastRestart).TotalMilliseconds < RESTART_COOLDOWN_MS) return;
                }

                RestartWorker(cameraIndex);
                _lastRestartAt[cameraIndex] = now;
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"TryRestartStalledCamera({cameraIndex})");
            }
        }

        private void RestartWorker(int index)
        {
            try
            {
                if (index < 0) return;
                if (index >= _supervisors.Count) return;

                // Dispose old supervisor to stop the process
                try { _supervisors[index]?.Dispose(); } catch { }

                string cameraWorkerPath = Path.Combine(Environment.CurrentDirectory, "CameraWorker.exe");
                if (!File.Exists(cameraWorkerPath))
                {
                    FileLogger.Log("RestartWorker: CameraWorker.exe not found");
                    return;
                }

                // Resolve STT and latest RTSP from DB
                int stt = index + 1;
                string rtspUrl = GetRtspUrlForStt(stt) ?? (index < ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count ? ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam[index] : null);
                if (string.IsNullOrWhiteSpace(rtspUrl))
                {
                    FileLogger.Log($"RestartWorker: Missing RTSP for camera index {index}, STT {stt}");
                    return;
                }

                string mmfName = $"Cam_{index}_MMF";
                string mutexName = $"Global\\Cam_{index}_Mutex";
                string camNameArg = $"camera_{index + 1}";
                string sttArg = stt.ToString();
                string connArg = $"\"{ClassSystemConfig.Ins?.m_ClsCommon?.connectionString}\"";
                string arguments = $"\"{rtspUrl}\" {mmfName} {mutexName} {camNameArg} {sttArg} {connArg}";

                var supervisor = new ProcessSupervisor(
                    loggerFactory: NullLoggerFactory.Instance,
                    processRunType: ProcessRunType.NonTerminating,
                    processPath: cameraWorkerPath,
                    arguments: arguments,
                    workingDirectory: Environment.CurrentDirectory
                );
                _supervisors[index] = supervisor;
                supervisor.Start();
                FileLogger.Log($"Restarted worker for camera {index + 1} (STT={stt})");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"RestartWorker({index})");
            }
        }

        private void InvalidateRegionCache()
        {
            try
            {
                lock (_regionLock)
                {
                    _regionDataByCam.Clear();
                }
                FileLogger.Log("Region cache invalidated: will reload 4-point regions from DB on next draw");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(InvalidateRegionCache));
            }
        }

        private string GetRtspUrlForStt(int stt)
        {
            try
            {
                if (stt <= 0) return null;
                string connStr = ClassSystemConfig.Ins?.m_ClsCommon?.connectionString;
                if (string.IsNullOrWhiteSpace(connStr)) return null;

                using (var conn = new MySql.Data.MySqlClient.MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "SELECT RTSP_URL FROM camera_list WHERE STT = @stt LIMIT 1";
                    using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@stt", stt);
                        var obj = cmd.ExecuteScalar();
                        return obj?.ToString()?.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, $"GetRtspUrlForStt({stt})");
                return null;
            }
        }

        public void ChangeCameraSpreadView(int cameraIndex, bool spreadOut)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => ChangeCameraSpreadView(cameraIndex, spreadOut)));
                    return;
                }

                if (spreadOut && cameraIndex >= 0)
                {
                    int row = cameraIndex / Col;
                    int col = cameraIndex % Col;

                    for (int i = 0; i < tableLayoutPanelCamera.RowCount; i++)
                    {
                        if (i == row)
                        {
                            tableLayoutPanelCamera.RowStyles[i] = new RowStyle(SizeType.Percent, 100f);
                        }
                        else
                        {
                            tableLayoutPanelCamera.RowStyles[i] = new RowStyle(SizeType.Absolute, 0f);
                        }
                    }

                    for (int i = 0; i < tableLayoutPanelDevice[row].ColumnCount; i++)
                    {
                        if (i == col)
                        {
                            tableLayoutPanelDevice[row].ColumnStyles[i] = new ColumnStyle(SizeType.Percent, 100f);
                        }
                        else
                        {
                            tableLayoutPanelDevice[row].ColumnStyles[i] = new ColumnStyle(SizeType.Absolute, 0f);
                        }
                    }

                    if (cameraIndex < _pictureboxes.Count)
                    {
                        _pictureboxes[cameraIndex].BorderStyle = BorderStyle.Fixed3D;
                    }
                }
                else
                {
                    for (int i = 0; i < tableLayoutPanelCamera.RowCount; i++)
                    {
                        tableLayoutPanelCamera.RowStyles[i] = new RowStyle(SizeType.Percent, 100f / tableLayoutPanelCamera.RowCount);
                    }

                    for (int row = 0; row < Row; row++)
                    {
                        for (int i = 0; i < tableLayoutPanelDevice[row].ColumnCount; i++)
                        {
                            tableLayoutPanelDevice[row].ColumnStyles[i] = new ColumnStyle(SizeType.Percent, 100f / tableLayoutPanelDevice[row].ColumnCount);
                        }
                    }

                    foreach (var pictureBox in _pictureboxes)
                    {
                        pictureBox.BorderStyle = BorderStyle.FixedSingle;
                    }
                }

                tableLayoutPanelCamera.PerformLayout();
                for (int i = 0; i < Row; i++)
                {
                    tableLayoutPanelDevice[i].PerformLayout();
                }

                Application.DoEvents();
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ChangeCameraSpreadView");
            }
        }

        #endregion

        #region Save/Load Config
        public void SaveDeviceConfig(bool ShowMessage)
        {
            string file_name = Directory.GetCurrentDirectory() + @"\Config Setting\DeviceConfig.ini";

            if (!System.IO.File.Exists(file_name))
            {
                System.IO.Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\Config Setting");
            }

            try
            {
                using (StreamWriter objWriter = new StreamWriter(file_name))
                {
                    objWriter.WriteLine("[PROGRAM NAME]  " + ClassCommon.ProgramName);
                    objWriter.WriteLine("[MACHINE NAME]  " + ClassCommon.MachineName);

                    // IP CAM VISION
                    //for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                    //    objWriter.WriteLine(String.Format("[IP CAM VISION {0}]  {1}", iControl + 1, ClassSystemConfig.Ins.m_ClsCommon.m_ListIPCAM[iControl]));
                    //for (int i = 0; i < ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count; i++)
                    //{
                    //    // Ghi vào t?p v?i tên camera t? 1 d?n 9
                    //    objWriter.WriteLine("[RTSP CAMERA " + (i + 1) + "]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam[i]);
                    //}

                    // LOGIN
                    objWriter.WriteLine("[LOGIN USER]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListUserLogin[0]);
                    objWriter.WriteLine("[LOGIN PASS]  " + "***");

                    // SAVING
                    objWriter.WriteLine("[IS SAVE IMG_LOCAL]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE GRAPHIC IMG LOCAL]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE LOG LOCAL]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE IMG_FTP]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_FTP ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE GRAPHIC IMG FTP]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_FTP ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE LOG FTP]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_FTP ? 1 : 0));

                    objWriter.WriteLine("[IS DELETE AUTO]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bAutoDeleteImg ? 1 : 0));
                    objWriter.WriteLine("[PERIOD DELETE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iPeriodDelete);
                    objWriter.WriteLine("[COMMON PATH]  " + ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath);

                    objWriter.WriteLine("[SHOW GRAPHIC]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic ? "1" : "0"));
                    objWriter.WriteLine("[SHOW ORIGIN]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bShowOrigin ? "1" : "0"));
                    objWriter.WriteLine("[SHOW PROGRESS STATUS]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bShowProgressStatus ? "1" : "0"));

                    objWriter.Close();
                }
                if (ShowMessage)
                {
                    MessageBox.Show("Saved Configurations");
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
            string file_name = Directory.GetCurrentDirectory() + @"\Config Setting\DeviceConfig.ini";

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
                        ClassCommon.ProgramName = ClassCommon.GetConfig(file_name, "PROGRAM NAME", "UT ALIGNMENT");
                        ClassCommon.MachineName = ClassCommon.GetConfig(file_name, "MACHINE NAME", "MC #2");

                        //for (int i = 0; i < 9; i++)
                        //{
                        //    // Ghi vào t?p v?i tên camera t? 1 d?n 9
                        //    ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Add("");
                        //    ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam[i] = ClassCommon.GetConfig(file_name, "RTSP CAMERA " + (i + 1), "");
                        //}

                        ClassSystemConfig.Ins.m_ClsCommon.m_ListUserLogin[0] = ClassCommon.GetConfig(file_name, "LOGIN USER", "Admin");
                        ClassSystemConfig.Ins.m_ClsCommon.m_ListPasswordLogin[0] = ClassCommon.GetConfig(file_name, "LOGIN PASS", "");

                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local = (ClassCommon.GetConfig(file_name, "IS SAVE IMG_LOCAL", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local = (ClassCommon.GetConfig(file_name, "IS SAVE GRAPHIC IMG LOCAL", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local = (ClassCommon.GetConfig(file_name, "IS SAVE LOG LOCAL", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_FTP = (ClassCommon.GetConfig(file_name, "IS SAVE GRAPHIC IMG FTP", "1").Trim() == "1") ? true : false;

                        ClassSystemConfig.Ins.m_ClsCommon.m_bAutoDeleteImg = (ClassCommon.GetConfig(file_name, "IS DELETE AUTO", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_iPeriodDelete = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "PERIOD DELETE", "30"), 15);
                        ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath = ClassCommon.GetConfig(file_name, "COMMON PATH", Directory.GetCurrentDirectory());

                        ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic = (ClassCommon.GetConfig(file_name, "SHOW GRAPHIC", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_bShowOrigin = false; // (ClassCommon.GetConfig(file_name, "SHOW ORIGIN", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_bShowProgressStatus = (ClassCommon.GetConfig(file_name, "SHOW PROGRESS STATUS", "0").Trim() == "1") ? true : false;

                        ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect = (ClassCommon.GetConfig(file_name, "AUTO RECONNECT", "0").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveByFTP = (ClassCommon.GetConfig(file_name, "IS_FTP_SAVING", "1").Trim() == "1") ? true : false;

                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeBetween2Trigger = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "TIMEOUT GET IMAGE", "1000"), 1000);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iModeTriggerLight = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "TRIGGER LIGHT MODE", "1"), 1);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeDelayTriggerCAM = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "DELAY TRIGGER CAM MS", "100"), 100);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeDelaySendReady = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "DELAY SEND READY MS", "50"), 50);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeoutCheckReady = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "TIMEOUT CHECK READY MS", "2000"), 2000);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "SAVING JPG MODE", "0"), 0);
                        ClassSystemConfig.Ins.m_ClsCommon.m_bCheckTimeoutReady = (ClassCommon.GetConfig(file_name, "CHECK TIMEOUT READY", "0").Trim() == "1") ? true : false;

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

        #region Fullscreen Control Methods
        private void ToggleFullscreen(int cameraIndex)
        {
            try
            {
                if (_isShuttingDown) return;

                if (cameraIndex < 0 || cameraIndex >= ActiveCameraCount)
                {
                    Console.WriteLine($"? Invalid camera index {cameraIndex + 1} (Available: 1-{ActiveCameraCount})");
                    return;
                }

                lock (_fullscreenLock)
                {
                    if (_isFullscreen)
                    {
                        if (_fullscreenCameraIndex == cameraIndex)
                        {
                            ExitFullscreen();
                        }
                        else
                        {
                            EnterFullscreen(cameraIndex);
                        }
                    }
                    else
                    {
                        EnterFullscreen(cameraIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ToggleFullscreen");
                Console.WriteLine($"? ToggleFullscreen error: {ex.Message}");
            }
        }

        private void EnterFullscreen(int cameraIndex)
        {
            try
            {
                if (cameraIndex < 0 || cameraIndex >= ActiveCameraCount) return;

                _isFullscreen = true;
                _fullscreenCameraIndex = cameraIndex;

                ChangeCameraSpreadView(cameraIndex, true);

                Console.WriteLine($"Camera {cameraIndex + 1} expanded to fullscreen (Press ESC to exit)");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "EnterFullscreen");
                Console.WriteLine($"? EnterFullscreen error: {ex.Message}");

                _isFullscreen = false;
                _fullscreenCameraIndex = -1;
            }
        }

        public void ExitFullscreen()
        {
            try
            {
                if (!_isFullscreen)
                {
                    return;
                }

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => ExitFullscreen()));
                    return;
                }

                _isFullscreen = false;
                _fullscreenCameraIndex = -1;

                ChangeCameraSpreadView(-1, false);

                Console.WriteLine($"? Exited fullscreen - Returned to grid view");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ExitFullscreen");
                Console.WriteLine($"? ExitFullscreen error: {ex.Message}");

                _isFullscreen = false;
                _fullscreenCameraIndex = -1;
            }
        }
        #endregion

        #region Helper and Utility Methods

        // Allow external/UI to adjust active camera count at runtime
        public void SetCameraCountOverride(int? count)
        {
            try
            {
                if (count.HasValue && count.Value <= 0) count = 1;
                _cameraCountOverride = count;
                FileLogger.Log($"SetCameraCountOverride: total={NumCameras}, active={ActiveCameraCount}");
                if (this.InvokeRequired)
                    this.BeginInvoke(new Action(LayoutCameraSpreadView));
                else
                    LayoutCameraSpreadView();
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, nameof(SetCameraCountOverride));
            }
        }

        public void ShowKeyboardShortcuts()
        {
            try
            {
                string shortcuts = $@"
?? CAMERA MANAGER - KEYBOARD SHORTCUTS:

?? CAMERA CONTROLS ({ActiveCameraCount} cameras available):
• 1-9: Toggle fullscreen for Camera 1-9 (OPTIMIZED)
  - Press number to enter fullscreen
  - Press same number again to exit fullscreen
  - Press different number to switch cameras
• F1-F12: Toggle fullscreen for Camera 1-12
• Numpad 1-9: Same as number keys 1-9
• ESC: Exit fullscreen mode (ALWAYS WORKS)
• Double-click: Toggle fullscreen for selected camera

?? SYSTEM CONTROLS:
• Ctrl+R: Reload camera list from database
• Ctrl+Alt+X: Emergency shutdown
• Ctrl+Shift+K: Force kill camera workers
• Ctrl+E: Force exit fullscreen (emergency)
• H: Show this help


?? OPTIMIZED FEATURES:
• Smart camera switching - no need to exit first
• Reliable ESC key - always exits fullscreen
• Number keys 1-{Math.Min(ActiveCameraCount, 9)} for quick access
";

                MessageBox.Show(shortcuts, "?? Camera Manager - Keyboard Shortcuts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                FileLogger.Log("?? Optimized keyboard shortcuts displayed to user");

                Console.WriteLine($"?? Help shown - Current config: {ActiveCameraCount} cameras in {Row}x{Col} grid");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ShowKeyboardShortcuts");
            }
        }

        public void DebugFullscreenState()
        {
            try
            {
                Console.WriteLine("?? === FULLSCREEN DEBUG INFO ===");
                Console.WriteLine($"?? _isFullscreen: {_isFullscreen}");
                Console.WriteLine($"?? _fullscreenCameraIndex: {_fullscreenCameraIndex}");
                Console.WriteLine($"?? NumCameras: {ActiveCameraCount}");
                Console.WriteLine("?? === END DEBUG INFO ===");

                FileLogger.Log($"DEBUG - Fullscreen: {_isFullscreen}, Camera: {_fullscreenCameraIndex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Debug error: {ex.Message}");
                FileLogger.LogException(ex, "DebugFullscreenState");
            }
        }

        public void ForceExitFullscreen()
        {
            try
            {
                Console.WriteLine("?? FORCE EXIT FULLSCREEN CALLED");
                FileLogger.Log("?? FORCE EXIT FULLSCREEN CALLED");

                _isFullscreen = false;
                _fullscreenCameraIndex = -1;

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => ForceExitFullscreen()));
                    return;
                }

                LayoutCameraSpreadView();

                Console.WriteLine("? FORCE EXIT FULLSCREEN COMPLETED");
                FileLogger.Log("? FORCE EXIT FULLSCREEN COMPLETED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ForceExitFullscreen error: {ex.Message}");
                FileLogger.LogException(ex, "ForceExitFullscreen");
            }
        }

        public void EmergencyShutdown()
        {
            try
            {
                FileLogger.Log("?? EMERGENCY SHUTDOWN INITIATED");
                _isShuttingDown = true;

                DisplayTimer?.Stop();
                _detectionTimer?.Stop();
                _detectionTimer?.Dispose();
                _detectionCleanupTimer?.Stop();
                _detectionCleanupTimer?.Dispose();

                ForceKillAllCameraWorkers();

                // Ensure all resources are disposed and cleared
                CleanupResources();

                FileLogger.Log("? Emergency shutdown completed");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "EmergencyShutdown");
            }
        }

        private static Bitmap ResizeToSquare(Bitmap src, int size)
        {
            if (src == null) return null;
            if (size <= 0) return (Bitmap)src.Clone();

            // Maintain aspect ratio, letterbox into square canvas
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

        // Threshold checks removed: draw all detections with fixed score.

        private void CleanupResources()
        {
            try
            {
                // Dispose supervisors
                if (_supervisors != null && _supervisors.Count > 0)
                {
                    foreach (var sup in _supervisors)
                    {
                        try { sup?.Dispose(); } catch { }
                    }
                    _supervisors.Clear();
                }

                // Dispose MMFs
                if (_mmfs != null && _mmfs.Count > 0)
                {
                    foreach (var mmf in _mmfs)
                    {
                        try { mmf?.Dispose(); } catch { }
                    }
                    _mmfs.Clear();
                }

                // Dispose Mutexes
                if (_mutexes != null && _mutexes.Count > 0)
                {
                    foreach (var m in _mutexes)
                    {
                        try { m?.Dispose(); } catch { }
                    }
                    _mutexes.Clear();
                }

                // Dispose latest frames
                lock (_frameStoreLock)
                {
                    foreach (var kv in _latestFrames)
                    {
                        try { kv.Value?.Dispose(); } catch { }
                    }
                    _latestFrames.Clear();
                }

                // Clear picture boxes and dispose old images
                foreach (var pictureBox in _pictureboxes)
                {
                    try
                    {
                        if (pictureBox != null && !pictureBox.IsDisposed)
                        {
                            var old = pictureBox.Image;
                            pictureBox.Image = null;
                            try { old?.Dispose(); } catch { }

                            if (pictureBox.Controls.Count > 0)
                            {
                                pictureBox.Controls[0].Visible = true;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "CleanupResources");
            }
        }

        private void ForceKillAllCameraWorkers()
        {
            try
            {
                FileLogger.Log("?? EMERGENCY: Force killing all CameraWorker processes...");

                var processes = System.Diagnostics.Process.GetProcessesByName("CameraWorker");
                foreach (var process in processes)
                {
                    try
                    {
                        FileLogger.Log($"?? Force killing CameraWorker process ID: {process.Id}");
                        process.Kill();
                        process.WaitForExit(1000);
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException(ex, $"ForceKill process {process.Id}");
                    }
                }

                FileLogger.Log("? Force kill completed");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "ForceKillAllCameraWorkers");
            }
        }

        public void ReloadCameraList()
        {
            try
            {
                Console.WriteLine("?? Reloading camera list from database...");
                LoadCameraList();
                // Thresholds are global; no per-camera reload needed
                // Invalidate region cache so any changes to 4-point region are picked up
                InvalidateRegionCache();

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => LayoutCameraSpreadView()));
                }
                else
                {
                    LayoutCameraSpreadView();
                }

                Console.WriteLine("? Camera list reloaded successfully");
                FileLogger.Log($"Camera list reloaded: {NumCameras} total, active={ActiveCameraCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error reloading camera list: {ex.Message}");
                FileLogger.LogException(ex, "ReloadCameraList");
            }
        }

        public void UpdateCameraLogInvoke(System.Windows.Forms.Control control)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    UpdateCameraLog();
                }));
            }
            else
            {
                UpdateCameraLog();
            }
        }

        public void UpdateCameraLog()
        {
            try
            {
                connection.Open();
                string query = "SELECT Camera, Time, Event FROM camera_log ORDER BY STT DESC";
                MySqlDataAdapter dataAdapter = new MySqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataAdapter.Fill(dataTable);
                dgviewLog.DataSource = dataTable;
                ClassSystemConfig.Ins.m_ClsFunc.FormatDataGridView(dgviewLog);

                dgviewLog.Columns["Camera"].HeaderText = "Camera";
                dgviewLog.Columns["Camera"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgviewLog.Columns["Camera"].Width = 80;
                dgviewLog.Columns["Time"].HeaderText = "Time";
                dgviewLog.Columns["Time"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgviewLog.Columns["Event"].HeaderText = "Event";
                dgviewLog.Columns["Event"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgviewLog.Columns["Event"].Width = 100;
            }
            catch (Exception ex)
            {
                MessageBox.Show("L?i k?t n?i: " + ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }

        // Process exit handlers
        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            FileLogger.Log("?? ProcessExit event triggered - Emergency cleanup");
            EmergencyShutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            FileLogger.LogException((Exception)e.ExceptionObject, "UnhandledException");
            FileLogger.Log("?? Unhandled exception - Emergency cleanup");
            EmergencyShutdown();
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            FileLogger.Log("?? ApplicationExit event triggered - Emergency cleanup");
            EmergencyShutdown();
        }

        private void SystemEvents_SessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            FileLogger.Log($"?? Windows session ending: {e.Reason} - Emergency cleanup");
            EmergencyShutdown();
        }

        // Camera system methods
        private void StartCameraSystem()
        {
            try
            {
                _isShuttingDown = false; // allow display/update after a previous Stop
                // Always refresh region overlay from DB on start
                InvalidateRegionCache();
                
                string cameraWorkerPath = Path.Combine(Environment.CurrentDirectory, "CameraWorker.exe");
                if (!File.Exists(cameraWorkerPath))
                {
                    string errorMsg = $"? CameraWorker.exe not found at: {cameraWorkerPath}";
                    FileLogger.Log(errorMsg);
                    Console.WriteLine(errorMsg);
                    return;
                }

                if (ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count == 0)
                {
                    LoadCameraList();
                }

                // Start at most 6 camera workers (limit live cameras)
                int actualCameraCount = Math.Min(6, Math.Min(ActiveCameraCount, ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam.Count));
                FileLogger.Log($"?? Starting {actualCameraCount} camera workers");

                // Reset last-frame timestamps so No-Signal can be evaluated from Start
                lock (_frameStoreLock)
                {
                    _lastFrameAt.Clear();
                }

                for (int i = 0; i < actualCameraCount; i++)
                {
                    string rtspUrl = ClassSystemConfig.Ins.m_ClsCommon.m_ListRtspCam[i];
                    string mmfName = $"Cam_{i}_MMF";
                    string mutexName = $"Global\\Cam_{i}_Mutex";

                    try
                    {
                        var mmf = MemoryMappedFile.CreateOrOpen(mmfName, MaxFrameSize, MemoryMappedFileAccess.ReadWrite);
                        _mmfs.Add(mmf);

                        using (var accessor = mmf.CreateViewAccessor())
                        {
                            accessor.Write(0, 0);
                            accessor.Write(4, 0);
                        }

                        _mutexes.Add(new Mutex(false, mutexName));

                        string camNameArg = $"camera_{i + 1}";
                        string sttArg = (i + 1).ToString();
                        string connArg = $"\"{ClassSystemConfig.Ins?.m_ClsCommon?.connectionString}\"";
                        string arguments = $"\"{rtspUrl}\" {mmfName} {mutexName} {camNameArg} {sttArg} {connArg}";
                        var supervisor = new ProcessSupervisor(
                            loggerFactory: NullLoggerFactory.Instance,
                            processRunType: ProcessRunType.NonTerminating,
                            processPath: cameraWorkerPath,
                            arguments: arguments,
                            workingDirectory: Environment.CurrentDirectory
                        );

                        _supervisors.Add(supervisor);
                        supervisor.Start();

                        FileLogger.Log($"?? Camera {i + 1} worker started");
                        Thread.Sleep(200);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException(ex, $"StartCameraSystem - Camera {i + 1}");
                        continue;
                    }
                }

                DisplayTimer.Interval = 10;
                DisplayTimer.Start();

                FileLogger.Log($"? Camera system started with {_supervisors.Count}/{actualCameraCount} workers");
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex, "StartCameraSystem");
            }
        }

        private void UpdatePictureBox(int index)
        {
            if (_isShuttingDown || index >= _pictureboxes.Count) return;

            try
            {
                var mutex = _mutexes[index];
                var mmf = _mmfs[index];
                var pictureBox = _pictureboxes[index];

                // Allow a tiny wait to reduce lost frames while writer holds the mutex
                if (mutex?.WaitOne(5) == true)
                {
                    try
                    {
                        using (var accessor = mmf.CreateViewAccessor())
                        {
                            int width = accessor.ReadInt32(0);
                            int height = accessor.ReadInt32(4);

                            if (width > 0 && height > 0 && width <= MaxFrameWidth && height <= MaxFrameHeight)
                            {
                                long frameSize = (long)width * height * 3;
                                byte[] frameData = new byte[frameSize];
                                accessor.ReadArray(8, frameData, 0, frameData.Length);

                                bool hasData = false;
                                for (int i = 0; i < Math.Min(1000, frameData.Length); i++)
                                {
                                    if (frameData[i] != 0)
                                    {
                                        hasData = true;
                                        break;
                                    }
                                }

                                if (hasData)
                                {
                                    var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                                    BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height),
                                        ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                                    try
                                    {
                                        int stride = bmpData.Stride;
                                        IntPtr scan0 = bmpData.Scan0;
                                        int srcLineSize = width * 3;

                                        for (int y = 0; y < height; y++)
                                        {
                                            int srcOffset = y * srcLineSize;
                                            IntPtr dstLineStart = scan0 + (y * stride);
                                            Marshal.Copy(frameData, srcOffset, dstLineStart, Math.Min(srcLineSize, stride));
                                        }
                                    }
                                    finally
                                    {
                                        bmp.UnlockBits(bmpData);
                                    }

                                    if (!_isShuttingDown && !pictureBox.IsDisposed)
                                    {
                                        if (pictureBox.Controls.Count > 0)
                                        {
                                            pictureBox.Controls[0].Visible = false; // hide overlay
                                        }

                                        var oldImage = pictureBox.Image;
                                        pictureBox.Image = bmp;
                                        oldImage?.Dispose();

                                        // Update last frame timestamp for No-Signal tracking
                                        lock (_frameStoreLock)
                                        {
                                            _lastFrameAt[index] = DateTime.Now;
                                        }

                                        // 👉 Ensure DetectionTimer has data
                                        lock (_frameStoreLock)
                                        {
                                            if (_latestFrames.ContainsKey(index))
                                            {
                                                _latestFrames[index]?.Dispose();
                                                _latestFrames[index] = (Bitmap)bmp.Clone();
                                            }
                                            else
                                            {
                                                _latestFrames.Add(index, (Bitmap)bmp.Clone());
                                            }
                                            // tăng sequence mỗi khi nhận frame mới
                                            if (_frameSeqByCam.TryGetValue(index, out var seq))
                                                _frameSeqByCam[index] = seq + 1;
                                            else
                                                _frameSeqByCam[index] = 1;
                                        }

                                        pictureBox.Invalidate();
                                    }
                                    else
                                    {
                                        bmp.Dispose();
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_isShuttingDown)
                {
                    FileLogger.LogException(ex, $"updating picture box {index}");
                }
            }
        }
        #endregion
    }
}

