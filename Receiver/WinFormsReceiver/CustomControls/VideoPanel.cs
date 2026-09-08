using ReceiverLib;
using ReceiverLib.WinForms;

namespace WinFormsReceiver.Components
{
    public unsafe class VideoPanel : Panel
    {
        private readonly WinFormsFrameView _frameView = new(Program.Context.VideoReceiver);

        public VideoPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);

            Program.Context.VideoReceiver.FrameReady += OnFrameReady;
        }

        private void OnFrameReady(object? sender, in VideoFrame frame)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() => Invalidate());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_frameView.TryGetBitmap(out Bitmap bitmap))
            {
                e.Graphics.DrawImage(bitmap, Point.Empty);
            }
        }
    }
}
