using System.Text;
using WinFormsReceiver.Context;

namespace WinFormsReceiver.CustomControls
{
    public class VideoPanelControl : Panel
    {
        private IVideo _video = null!;
        private Bitmap? _latestFrame;
        private readonly object _frameLock = new();
        private readonly StringBuilder _sb = new();
        private Size? _lastFrameSize;

        public event Action<Size>? VideoSizeChanged;

        public VideoPanelControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void Initialize(IVideo? video)
        {
            ArgumentNullException.ThrowIfNull(video, nameof(video));
            _video = video;
            _video.FrameReady = OnFrameReady;
        }

        private void OnFrameReady(Bitmap frame)
        {
            // 'frame' wraps the Video class's own pinned buffer, which gets overwritten by
            // the next callback — copy it out. NOTE: frame.Clone() looked like the obvious
            // way to do this but silently does NOT deep-copy pixel data for a Bitmap backed
            // by external unmanaged memory (our scan0 pointer ctor in Video.cs) — it returns
            // a distinct object each time but with frozen, first-frame content, which is why
            // playback appeared to freeze on frame one. A real DrawImage blit into a normal,
            // GDI+-owned Bitmap does copy the live pixels.
            var copy = new Bitmap(frame.Width, frame.Height, frame.PixelFormat);
            using (var g = Graphics.FromImage(copy))
            {
                g.DrawImageUnscaled(frame, 0, 0);
            }

            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = copy;
            }

            var frameSize = new Size(frame.Width, frame.Height);
            if (_lastFrameSize != frameSize)
            {
                _lastFrameSize = frameSize;
                if (IsHandleCreated)
                    BeginInvoke((Action)(() => VideoSizeChanged?.Invoke(frameSize)));
            }

            // Marshal to UI thread to repaint
            if (IsHandleCreated)
                BeginInvoke((Action)(() => Invalidate()));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            lock (_frameLock)
            {
                if (_latestFrame != null)
                {
                    var g = e.Graphics;
                    g.DrawImage(_latestFrame, ClientRectangle);
                    _sb.Clear();

                    _sb.AppendLine($"{_video.Stats.Width} x {_video.Stats.Height} ({Helpers.GetAspectRatio(_video.Stats.Width, _video.Stats.Height)}) @ {_video.Stats.Fps:0.0} fps");
                    _sb.AppendLine($"{_video.Stats.TimePlayed.ToString(@"hh\:mm\:ss\.f")}");
                    _sb.AppendLine($"Frames: {_video.Stats.FramesDecoded}");

                    g.FillRectangle(new SolidBrush(Color.FromArgb(150, 0, 0, 0)), new Rectangle(10, 10, 320, 80));
                    g.DrawString(_sb.ToString(), Font, Brushes.White, 20, 20);
                }
            }
        }
    }
}
