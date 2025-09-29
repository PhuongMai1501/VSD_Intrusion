using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DKVN
{
    public partial class FormConfirmVision : Form
    {
        public int CameraIndex { get; set; } = -1;
        public string AlarmText { get; set; } = "Vision detection Warning. Please confirm the results below.";
        public Action<int> OnConfirm { get; set; }

        public FormConfirmVision()
        {
            InitializeComponent();
        }

        public void InitializeUI(int row, int col)
        {
            IsConfirmCompleted = false;
            CameraIndex = (row >= 0) ? row : col; // backward compat if caller uses (row,col)
            try { lbAlarmMessage.Text = AlarmText; } catch { }
        }

        public void SetAlarm(string text, int cameraIndex)
        {
            try
            {
                AlarmText = text ?? AlarmText;
                CameraIndex = cameraIndex;
                lbAlarmMessage.Text = AlarmText;
            }
            catch { }
        }

        public bool IsConfirmCompleted = false;
        public int RESULT_CONFIRM = 0;
        private void btnNG_Click(object sender, EventArgs e)
        {
            IsConfirmCompleted = true;
            this.Hide();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            IsConfirmCompleted = true;
            try { OnConfirm?.Invoke(CameraIndex); } catch { }
            this.Hide();
        }

        private void FormConfirmVision_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
