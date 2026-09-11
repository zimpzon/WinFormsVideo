using System.Runtime.InteropServices;

namespace WinFormsReceiver
{
    public partial class OverlayForm : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;

        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;

            public SIZE(int cx, int cy)
            {
                this.cx = cx;
                this.cy = cy;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hWnd,
            IntPtr hdcDst,
            ref POINT pptDst,
            ref SIZE psize,
            IntPtr hdcSrc,
            ref POINT pptSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr ho);

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                cp.ExStyle |= WS_EX_LAYERED;

                return cp;
            }
        }

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
        }

        public void UpdateOverlay(Bitmap bitmap)
        {
            var screenPosition = Location;

            var screenDc = GetDC(IntPtr.Zero);
            var memDc = CreateCompatibleDC(screenDc);

            var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            var oldBitmap = SelectObject(memDc, hBitmap);

            try
            {
                var size = new SIZE(bitmap.Width, bitmap.Height);
                var sourcePoint = new POINT(0, 0);
                var destinationPoint = new POINT(
                    screenPosition.X,
                    screenPosition.Y);

                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destinationPoint,
                    ref size,
                    memDc,
                    ref sourcePoint,
                    0,
                    ref blend,
                    ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
