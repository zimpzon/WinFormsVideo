using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private LogForm _logForm;
        private readonly IVideo _video;
        private readonly int _videoRightMargin;
        private readonly int _videoBottomMargin;

        public MainForm(IVideo? video, LogForm logForm)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));
            ArgumentNullException.ThrowIfNull(logForm, nameof(logForm));
            InitializeComponent();

            _video = video;
            _videoRightMargin = ClientSize.Width - VideoPanel.Right;
            _videoBottomMargin = ClientSize.Height - VideoPanel.Bottom;

            VideoPanel.Initialize(video);
            VideoPanel.VideoSizeChanged += VideoPanel_VideoSizeChanged;

            _logForm = logForm;
            _logForm.LogMessage($"App started");
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            btnPlayVideo.Enabled = false;
            _logForm.LogMessage($"Starting video from {tbVideoUri.Text}");
            _video.Play(new Uri(tbVideoUri.Text));
        }

        private void btnShowLog_Click(object sender, EventArgs _)
        {
            _logForm.Show();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs _)
        {
            _video.Stop();
        }

        private void VideoPanel_VideoSizeChanged(Size newSize)
        {
            VideoPanel.Size = newSize;
            ClientSize = new Size(VideoPanel.Right + _videoRightMargin, VideoPanel.Bottom + _videoBottomMargin);
        }
    }
}
