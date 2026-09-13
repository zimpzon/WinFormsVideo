using Microsoft.Extensions.DependencyInjection;
using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private LogForm _logForm = new LogForm();
        private readonly IVideo _video;
        private readonly int _videoRightMargin;
        private readonly int _videoBottomMargin;

        public MainForm()
        {
            InitializeComponent();

            _videoRightMargin = ClientSize.Width - VideoPanel.Right;
            _videoBottomMargin = ClientSize.Height - VideoPanel.Bottom;

            _video = WinFormsContext.Instance.ServiceProvider.GetRequiredService<IVideo>();
            VideoPanel.VideoSizeChanged += VideoPanel_VideoSizeChanged;

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
