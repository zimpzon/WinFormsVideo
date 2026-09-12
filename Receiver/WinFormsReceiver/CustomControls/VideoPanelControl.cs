using System.Text;
using WinFormsReceiver.Context;

namespace WinFormsReceiver.CustomControls
{
    public class VideoPanelControl : Panel
    {
        private IWinFormsContext _context = null!;
        private Bitmap? _latestFrame;
        private readonly object _frameLock = new();
        private readonly StringBuilder _sb = new();

        public VideoPanelControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void Initialize()
        {
            _context = WinFormsContext.Instance;
            _context.Video.FrameReady = OnFrameReady;
        }

        private void OnFrameReady(Bitmap frame)
        {
            _sb.Clear();

            _sb.AppendLine($"{_context.Video.Stats.Width} x {_context.Video.Stats.Height} @ {_context.Video.Stats.Fps:0.0} fps");
            _sb.AppendLine($"{_context.Video.Stats.TimePlayed}:0.0");

            using (var g = Graphics.FromImage(frame))
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(150, 0, 0, 0)), new Rectangle(5, 5, 300, 80));
                g.DrawString(_sb.ToString(), Font, Brushes.White, 10, 10);
            }

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

            // Marshal to UI thread to repaint
            if (IsHandleCreated)
                BeginInvoke((Action)(() => Invalidate()));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            lock (_frameLock)
            {
                if (_latestFrame != null)
                    e.Graphics.DrawImage(_latestFrame, ClientRectangle);
            }
        }
    }
}
