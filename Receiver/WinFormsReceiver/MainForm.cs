using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private LogForm _logForm = new LogForm();
        private readonly IWinFormsContext _context;
        private readonly int _videoRightMargin;
        private readonly int _videoBottomMargin;

        public MainForm()
        {
            InitializeComponent();

            _videoRightMargin = ClientSize.Width - VideoPanel.Right;
            _videoBottomMargin = ClientSize.Height - VideoPanel.Bottom;

            _context = WinFormsContext.Instance;
            VideoPanel.Initialize();
            VideoPanel.VideoSizeChanged += VideoPanel_VideoSizeChanged;
            VideoPanel.SizeChanged += VideoPanel_SizeChanged;
            UpdateSizesPanel();

            _logForm.LogMessage($"App started");
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            _logForm.LogMessage($"Starting video from {tbVideoUri.Text}");
            _context.Video.Play(new Uri(tbVideoUri.Text));
        }

        private void btnShowLog_Click(object sender, EventArgs _)
        {
            _logForm.Show();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs _)
        {
            _context.Video.Stop();
        }

        void UpdateSizesPanel()
        {
            labSizes.Text = $"Shown size: {VideoPanel.ClientSize.Width} x {VideoPanel.ClientSize.Height} ({Helpers.GetAspectRatio(VideoPanel.ClientSize.Width, VideoPanel.ClientSize.Height)})";
        }

        private void VideoPanel_SizeChanged(object? sender, EventArgs e)
        {
            UpdateSizesPanel();
        }

        private void VideoPanel_VideoSizeChanged(Size newSize)
        {
            VideoPanel.Size = newSize;
            ClientSize = new Size(VideoPanel.Right + _videoRightMargin, VideoPanel.Bottom + _videoBottomMargin);
        }

    }
}
