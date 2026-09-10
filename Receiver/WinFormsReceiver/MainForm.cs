using LibVLCSharp.Shared;
using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private LogForm _logForm = new LogForm();

        public MainForm()
        {
            InitializeComponent();
            Core.Initialize();
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            videoView1.MediaPlayer = WinFormsContext.Instance.Video.MediaPlayer;
            WinFormsContext.Instance.Video.Play(new Uri("rtsp://127.0.0.1:8554/live"));
        }

        private void btnShowLog_Click(object sender, EventArgs e)
        {
            _logForm.Show();
        }
    }
}
