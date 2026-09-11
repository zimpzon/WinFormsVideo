using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System.Drawing.Imaging;
using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private OverlayForm _overlay = new OverlayForm();
        private LogForm _logForm = new LogForm();
        private readonly IWinFormsContext _context;
        private long videoTimeFirstFrame = -1;

        public MainForm()
        {
            InitializeComponent();
            Core.Initialize();
            _context = WinFormsContext.Instance;

            // Huge hack to create the log forms control handle so we can log to it before it is otherwise shown.
            var oldPos = _logForm.Location;
            _logForm.Location = new Point(-10000, -10000);
            _logForm.Show();
            _logForm.Location = oldPos;
            _logForm.Hide();

            PositionOverlay();
            _overlay.Show();

            using var bitmap = new Bitmap(
                _overlay.Width,
                _overlay.Height,
                PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.DrawString("10N 213.2S", Font, Brushes.Black, new PointF(102, 102));
                g.DrawString("10N 213.2S", Font, Brushes.White, new PointF(100, 100));

                using var semi = new SolidBrush(Color.FromArgb(150, 0, 255, 0));
                g.FillRectangle(semi, new Rectangle(0, 0, 10000, 10000));
            }

            _overlay.UpdateOverlay(bitmap);
        }

        void PositionOverlay()
        {
            _overlay.Location = videoView1.PointToScreen(Point.Empty);
            _overlay.Size = videoView1.Size;
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            _logForm.LogMessage("Starting video");
            videoView1.MediaPlayer = _context.Video.MediaPlayer;
            _context.Video.Play(new Uri(tbVideoUri.Text));
            _context.Video.MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
        }

        private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            Invoke(() =>
            {
                if (videoTimeFirstFrame == -1)
                {
                    videoTimeFirstFrame = e.Time;
                }

                long videoTime = e.Time - videoTimeFirstFrame;
                TimeSpan ts = TimeSpan.FromMilliseconds(videoTime);
                labPlayTime.Text = ts.ToString();
            });
        }

        private void btnShowLog_Click(object sender, EventArgs _)
        {
            _logForm.Show();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs _)
        {
            _context.Video.Stop();
        }

        private void videoView1_Move(object sender, EventArgs e)
        {
            PositionOverlay();
        }

        private void videoView1_Resize(object sender, EventArgs e)
        {
            PositionOverlay();
        }

        private void MainForm_Move(object sender, EventArgs e)
        {
            PositionOverlay();
        }
    }
}
