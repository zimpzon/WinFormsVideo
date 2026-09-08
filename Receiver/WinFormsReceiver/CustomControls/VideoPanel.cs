using ReceiverLib;
using ReceiverLib.WinForms;

namespace WinFormsReceiver.Components
{
    public class VideoPanel : Panel
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
            e.Graphics.Clear(Color.FromArgb(60, 60, 60));

            if (_frameView.TryGetBitmap(out Bitmap bitmap))
            {
                float scaleX = (float)ClientSize.Width / bitmap.Width;
                float scaleY = (float)ClientSize.Height / bitmap.Height;

                // Use the smaller scale so the entire image fits.
                float scale = Math.Min(scaleX, scaleY);

                int width = (int)(bitmap.Width * scale);
                int height = (int)(bitmap.Height * scale);

                // Center the image in the panel.
                int x = (ClientSize.Width - width) / 2;
                int y = (ClientSize.Height - height) / 2;

                e.Graphics.DrawImage(bitmap, new Rectangle(x, y, width, height));
            }
        }
    }
}
