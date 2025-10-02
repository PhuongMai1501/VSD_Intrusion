using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Net.Http;

namespace CameraManager
{
    public class ClassCommon
    {
        #region Common variables
        public const int MaxUserControl = 1;
        public const int MaxDevice = 1;
        public static string ProgramName = "TAPE INSPECTION";
        public const string ProgramVersion = "VER.3.0.1-UpdateConfirmVision&FixDisconnect";
        public const string ProgramReleasedDate = "08/07/2024";
        public static string StrLoadingFormName = "LOADING...";
        public static string MachineName = "TAPE INSPECTION";
        public string angle_move ;

        // CAMERA VISION
        public List<string> m_ListRtspCam = new List<string>(10);

        public int m_iIndexControl { get; set; }
        public int m_indexView { get; set; }

        //DATA BASE
        public string connectionString = "Server=localhost;Database=action_recog;Uid=root;Pwd=123456;";

        public string m_SavePath { get; set; }

        public int m_iFormatSendMessage = 0; // 0: Telegram, 1: Discord, 2: WhatsApp, 3: Zalo
        public int b_ByPassAlarm = 0;

        // USER CONTROL
        public string[] m_ListNameControl = new string[MaxUserControl];
        public bool[] m_ListEnableControl = new bool[MaxUserControl];

        public double[] m_dListExposure = new double[MaxUserControl];
        public double[] m_dListGain = new double[MaxUserControl];
        public double[] m_dListTriggerDelayus = new double[MaxUserControl];

        // LOGIN
        public string[] m_ListUserLogin = new string[3];
        public string[] m_ListPasswordLogin = new string[3];
        public bool[] m_bLogInMode = new bool[3];
       public enum LOGIN_LEVEL
       {
           OPERATOR = 0,
           ENGINEER,
           ADMIN
       }


        // Server/Client
        public string url_Server = "http://127.0.0.1:8000/predict";

        // Saving Params
        public string m_CommonPath { get; set; }
        public bool IsSaveImgOKNG_Local { get; set; }
        public bool IsSaveLog_Local { get; set; }
        public bool IsSaveImgGraphic_Local { get; set; }
        public bool IsSaveImgOKNG_FTP { get; set; }
        public bool IsSaveLog_FTP { get; set; }
        public bool IsSaveImgGraphic_FTP { get; set; }
        public bool IsSaveByFTP { get; set; }

        public bool m_bAutoDeleteImg { get; set; }
        public int m_iPeriodDelete { get; set; }
        public bool m_bAutoReconnect { get; set; }

        // Vidi
        public string m_strVidiPath { get; set; }

        // Control Light
        public int m_iModeTriggerLight { get; set; }
        public int m_iTimeBetween2Trigger { get; set; }
        public int m_iTimeDelayTriggerCAM { get; set; }
        public int m_iTimeDelaySendReady = 50;
        public int m_iTimeoutCheckReady = 2000;
        public bool m_bCheckTimeoutReady = false;

        // Display Option
        public bool m_bShowGraphic { get; set; }
        public bool m_bShowOrigin { get; set; }
        public bool m_bShowProgressStatus { get; set; }

        public int[] m_iModeResultReturn = new int[MaxUserControl];
        public int m_iModeProtocol = 0; // 0: TCP/IP   1: SLMP
        public double m_dThresholdLimit { get; set; }

        public static string m_strLoadingValue = "";
        public int m_iFormatSavingMode = 1;

        // PLC Address
        public string m_strAddrCalibPLC = "D28500";
        public string m_strAddrCalibVS = "D29500";

        // Light Controller
        public string[] m_ListIPLightCtrl = new string[2];
        public int[] m_ListPortLightCtrl = new int[2];
        public bool[] m_ListEnableLightCtrl = new bool[2];
        public int[] m_ListLightCH1 = new int[4];
        public int[] m_ListLightCH2 = new int[4];
        public string[] m_ListLightCHName1 = new string[4];
        public string[] m_ListLightCHName2 = new string[4];


        public bool m_bShowAliveLog { get; set; }

        public SystemStatus _Status = new SystemStatus();
        public ClassResultCounter ClsCount = new ClassResultCounter();

        // Detection config
        public int DetectionInputSize { get; set; } = 640; // default 1280x1280 for API input
        #endregion

        #region Control Invoke
        public static void ControlTextInvoke(System.Windows.Forms.Control control, string text)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    control.Text = text;
                }));
            }
            else
            {
                control.Text = text;
            }
        }
        public enum STATUS_COLOR
        {
            GREY,
            GREEN,
            RED,
            NONE
        }
        public static void ControlStatusInvoke(System.Windows.Forms.Label control, STATUS_COLOR status)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    switch (status)
                    {
                        case STATUS_COLOR.GREY:
                            //control.Image = DKVN.Properties.Resources.NONE;
                            break;
                        case STATUS_COLOR.GREEN:
                            //control.Image = DKVN.Properties.Resources.ON;
                            break;
                        case STATUS_COLOR.RED:
                            //control.Image = DKVN.Properties.Resources.OFF;
                            break;
                        default:
                            break;
                    }
                }));
            }
            else
            {
                switch (status)
                {
                    case STATUS_COLOR.GREY:
                        //control.Image = DKVN.Properties.Resources.NONE;
                        break;
                    case STATUS_COLOR.GREEN:
                        //control.Image = DKVN.Properties.Resources.ON;
                        break;
                    case STATUS_COLOR.RED:
                        //control.Image = DKVN.Properties.Resources.OFF;
                        break;
                    default:
                        break;
                }
            }
        }

        public static void ControlBackColorInvoke(System.Windows.Forms.Control control, System.Drawing.Color color)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    control.BackColor = color;
                }));
            }
            else
            {
                control.BackColor = color;
            }
        }
        public static void ControlForeColorInvoke(System.Windows.Forms.Control control, System.Drawing.Color color)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    control.ForeColor = color;
                }));
            }
            else
            {
                control.ForeColor = color;
            }
        }
        public static void ControlBackColorInvoke(System.Windows.Forms.Control control, bool bSetON)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    control.BackColor = bSetON ? Color.Lime : Color.FromArgb(50, 53, 52);
                }));
            }
            else
            {
                control.BackColor = bSetON ? Color.Lime : Color.FromArgb(50, 53, 52);
            }
        }
        public static void CheckBoxInvoke(System.Windows.Forms.CheckBox control, bool bChecked)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    control.Checked = bChecked;
                }));
            }
            else
            {
                control.Checked = bChecked;
            }
        }
        public static bool CheckBoxValueInvoke(System.Windows.Forms.CheckBox control)
        {
            bool bRet = false;
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    bRet =  control.Checked;
                }));
            }
            else
            {
                bRet =  control.Checked;
            }
            return bRet;
        }
        public static bool RadioBtnValueInvoke(System.Windows.Forms.RadioButton control)
        {
            bool bRet = false;
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    bRet = control.Checked;
                }));
            }
            else
            {
                bRet = control.Checked;
            }
            return bRet;
        }
        public static void PictureBoxInvoke(System.Windows.Forms.PictureBox control, Bitmap bitmap)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new MethodInvoker(delegate
                {
                    control.Image = bitmap;
                }));
            }
            else
            {
                control.Image = bitmap;
            }
        }
        public static void ProgressBarInvoke(ProgressBar progressBar, int value)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new MethodInvoker(delegate
                {
                    if (value <= progressBar.Maximum && value >= progressBar.Minimum)
                        progressBar.Value = value;
                    else
                        if (value > progressBar.Maximum)
                            progressBar.Value = progressBar.Maximum;
                        else
                            progressBar.Value = progressBar.Minimum;
                }));
            }
            else
            {
                if (value <= progressBar.Maximum && value >= progressBar.Minimum)
                    progressBar.Value = value;
                else
                    if (value > progressBar.Maximum)
                        progressBar.Value = progressBar.Maximum;
                    else
                        progressBar.Value = progressBar.Minimum;
            }
        }
        //public static void PerformSafely(this Control target, Action action)
        //{
        //    if (target.InvokeRequired)
        //    {
        //        target.Invoke(action);
        //    }
        //    else
        //    {
        //        action();
        //    }
        //}
        public int ConvertStringToInt(string strInt, int iDefaultValue)
        {
            int iRet;
            try
            {
                double dTemp = double.Parse(strInt);
                iRet = (int)dTemp;
            }
            catch { iRet = iDefaultValue; }
            return iRet;
        }
        public double ConvertStringDouble(string strDouble, double fDefaultValue)
        {
            double fRet;
            try
            {
                //fRet = Int32.Parse(strDouble, System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowThousands); //Int32.Parse(strDouble, System.Globalization.CultureInfo.InvariantCulture);
                if (strDouble.Contains(","))
                    strDouble = strDouble.Replace(",", ".");
                fRet = double.Parse(strDouble);
            }
            catch { fRet = fDefaultValue; }
            return fRet;
        }

        #endregion

        #region Save Get Parametser Logs
        private static string Getkeyword(string fromLine)
        {
            string data = "";
            if (fromLine.Contains("[") && fromLine.Contains("]"))
                data = fromLine.Split('[', ']')[1];
            return data;
        }
        private static string GetValueKeyword(string fromLine, string keyword)
        {
            string data = "";
            if (fromLine.Contains("[") && fromLine.Contains("]"))
            {
                data = fromLine.Split('[', ']')[2];
                data = data.Trim();
            }
            return data;
        }
        public static string GetConfig(string file_name_ini, string key_word, string strDefault)
        {
            string strRet = strDefault;
            if (System.IO.File.Exists(file_name_ini))
            {
                string[] arrLine = File.ReadAllLines(file_name_ini);
                for (int i = 0; i < arrLine.Length; i++)
                {
                    string keyword = Getkeyword(arrLine[i]);
                    if (key_word.ToUpper().Trim() == keyword.ToUpper().Trim())
                    {
                        strRet = GetValueKeyword(arrLine[i], key_word);
                    }
                }
                if (strRet.Trim() == "")
                    strRet = strDefault;
            }

            return strRet;
        }
        public static void UpdateConfig(string file_name_ini, string key_word, string NewValue)
        {
            if (!System.IO.File.Exists(file_name_ini))
            {
                System.IO.File.Create(file_name_ini);
            }

            string[] arrLine = File.ReadAllLines(file_name_ini);
            bool IsKeywordExist = false;
            for (int i = 0; i < arrLine.Length; i++)
            {
                string keyword = Getkeyword(arrLine[i]);
                if (key_word.ToUpper().Trim() == keyword.ToUpper().Trim())
                {
                    arrLine[i] = "[" + keyword + "] " + NewValue;
                    IsKeywordExist = true;
                    break;
                }
            }

            List<string> listTemp = new List<string>();
            listTemp.AddRange(arrLine);
            if (IsKeywordExist == false)
            {
                listTemp.Add("[" + key_word + "] " + NewValue);
            }
            File.WriteAllLines(file_name_ini, listTemp.ToArray());
        }
        #endregion

        #region Ping Ethernet Respond
        public bool PingRespond(string ip, int timeout, int iRetry)
        {
            bool bRet = true;

            // Try Ping To Device
            Ping pingSender = new Ping();
            PingOptions option = new PingOptions();
            option.DontFragment = true;
            int iCnt = 0;
            byte[] buff = Encoding.ASCII.GetBytes("ping test");
            if (iRetry > 1)
            {
                for (int i = 0; i < iRetry; i++)
                {
                    PingReply pingRep = pingSender.Send(ip, timeout, buff, option);
                    if (pingRep.Status == IPStatus.Success)
                    {
                        iCnt++;
                        bRet &= true;
                    }
                    else
                    {
                        bRet &= false;
                    }
                }
            }
            else
            {
                PingReply pingRep = pingSender.Send(ip, timeout, buff, option);
                if (pingRep.Status == IPStatus.Success)
                {
                    iCnt++;
                    bRet &= true;
                }
                else
                {
                    bRet &= false;
                }
            }

            if (bRet == false && iRetry - iCnt < 2)
                bRet = true;

            return bRet;
        }
        public bool PingRespond(string ip, int timeout)
        {
            bool bRet = true;

            // Try Ping To Device
            Ping pingSender = new Ping();
            PingOptions option = new PingOptions();
            try
            {
                option.DontFragment = true;
                byte[] buff = Encoding.ASCII.GetBytes("ping");
                PingReply pingRep = pingSender.Send(ip, timeout, buff, option);
                if (pingRep.Status == IPStatus.Success)
                {
                    bRet = true;
                }
                else
                {
                    bRet = false;
                }
            }
            catch
            {
                bRet = false;
            }
            return bRet;
        }
        #endregion

        #region Show MessageBox
        public static void ShowMessageBoxShort(string message, string caption, int mSecond)
        {
            var w = new Form() { Size = new Size(0, 0) };
            Task.Delay(TimeSpan.FromMilliseconds(mSecond))
                .ContinueWith((t) => w.Close(), TaskScheduler.FromCurrentSynchronizationContext());


            MessageBox.Show(w, message, caption, MessageBoxButtons.OK);
        }
        public static void ShowMessageBox(string message, string captain)
        {
            //FormMessage frmMessage = new FormMessage();
            //frmMessage.SetMessageData(captain, message, FormMessage.MessageButton.OK);
            //frmMessage.Show();
        }

        #endregion

        #region Add Graphic
        //public static void SetGraphicStaticLabel(Cognex.VisionPro.CogRecordDisplay _cogDisplay, int size, Cognex.VisionPro.CogColorConstants color, string str, double X, double Y)
        //{
        //    using (Cognex.VisionPro.CogGraphicLabel _CogGraphicLabel = new Cognex.VisionPro.CogGraphicLabel())
        //    {

        //        _CogGraphicLabel.Color = color;
        //        _CogGraphicLabel.Font = new Font(_CogGraphicLabel.Font.Name, size, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        //        _CogGraphicLabel.Text = str;
        //        _CogGraphicLabel.X = X;
        //        _CogGraphicLabel.Y = Y;
        //        _cogDisplay.StaticGraphics.Add(_CogGraphicLabel, "LabelS");
        //    }
        //}
        #endregion

        #region Convert ByteString to ASCII
        private int ConvertCharHexToDecimal(string strHex)
        {
            switch (strHex.ToUpper().Trim())
            {
                case "0": return 0; break;
                case "1": return 1; break;
                case "2": return 2; break;
                case "3": return 3; break;
                case "4": return 4; break;
                case "5": return 5; break;
                case "6": return 6; break;
                case "7": return 7; break;
                case "8": return 8; break;
                case "9": return 9; break;
                case "A": return 10; break;
                case "B": return 11; break;
                case "C": return 12; break;
                case "D": return 13; break;
                case "E": return 14; break;
                case "F": return 15; break;
                default: return 0; break;

            }
        }
        public string ConvertByteStringToASCII(string strBytes)
        {
            string strRet = "";
            try
            {
                // Split each 2 characters
                List<string> listTemp = new List<string>();
                char[] ch = new char[2];
                for (int i = 0; i < strBytes.Length; i++)
                {
                    ch[0] = strBytes[i];
                    ch[1] = (i + 1 < strBytes.Length) ? strBytes[i + 1] : '\0';
                    string str = new String(ch);
                    listTemp.Add(str);
                    i += 1;
                }

                // Convert to string
                List<string> strTemp2 = new List<string>();
                for (int i = 0; i < listTemp.Count; i++)
                {
                    int iDec = 16 * ConvertCharHexToDecimal(listTemp[i][0].ToString()) + ConvertCharHexToDecimal(listTemp[i][1].ToString());

                    strTemp2.Add(((char)iDec).ToString());
                    strRet += strTemp2[i];
                }

                // Return Result

            }
            catch { }
            return strRet;
        }
        #endregion

        #region XML Read/Write Object
        public static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                writer = new StreamWriter(filePath, append);
                serializer.Serialize(writer, objectToWrite);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
        public static T ReadFromXmlFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                reader = new StreamReader(filePath);
                return (T)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        #endregion

        #region Loading Form
        private FormLoading m_FrmLoading = new FormLoading();
        private Thread _threadLoad;
        private void LoadingForm()
        {
            try
            {
                m_FrmLoading = new FormLoading();
                Application.Run(m_FrmLoading);

            }
            catch (System.Threading.ThreadAbortException ex)
            {
                throw ex;
            }

        }

        public void StartLoadingForm()
        {
            _threadLoad = new Thread(new ThreadStart(LoadingForm));
            _threadLoad.Start();
        }
        public void StopLoadingForm()
        {
            try
            {
                //m_FrmLoading.HideForm();
                m_FrmLoading.Invoke(new MethodInvoker(delegate { m_FrmLoading.Hide(); }));

                if (_threadLoad != null)
                {
                    _threadLoad.Abort();
                }

            }
            catch
            {

            }

        }
        #endregion

        #region Loading Startup Form
        Thread m_threadLoadingStartupForm;
        FormStartupLoading m_FrmStartupLoad;
        public void LoadingStartupForm()
        {
            try
            {
                m_FrmStartupLoad = new FormStartupLoading();
                Application.Run(m_FrmStartupLoad);

            }
            catch (System.Threading.ThreadAbortException ex)
            {
                Console.WriteLine("=>Throw: " + ex.Message);
                throw ex;

            }
        }
        public void StartupLoadingForm()
        {
            m_threadLoadingStartupForm = new Thread(new ThreadStart(LoadingStartupForm));
            m_threadLoadingStartupForm.Start();

        }
        public void StopStartupLoadingForm()
        {
            try
            {
                m_FrmStartupLoad.Invoke(new MethodInvoker(delegate { m_FrmStartupLoad.Hide(); }));

                if (m_threadLoadingStartupForm != null)
                {
                    m_threadLoadingStartupForm.Abort();
                }
            }
            catch
            {

            }

        }
        #endregion
    }
    public class SystemStatus
    {
        //PLC
        public bool PLC_Alive { get; set; }
        public bool PLC_Reset { get; set; }
        public bool PLC_ConfirmReset { get; set; }

        //VISION
        public bool CameraConnected { get; set; }
        public bool VISION_Alive { get; set; }
        public bool VISION_Ready { get; set; }
        public bool VISION_Busy { get; set; }
        public bool VISION_Error { get; set; }
        public bool VISION_Reset { get; set; }
        public bool VISION_ConfirmReset { get; set; }
        //SYSTEM
        public bool PROGRAM_LOOP_RUNNING { get; set; }
        public bool PROGRAM_STARTED { get; set; }

    }
    public class ClassResultCounter
    {
        // Result Count
        public int[] ListCountResult = new int[3];
    }
    //class Detection
    //{
    //    public string label { get; set; }
    //    public double x1 { get; set; }
    //    public double y1 { get; set; }
    //    public double x2 { get; set; }
    //    public double y2 { get; set; }
    //    public double score { get; set; }
    //}

}
