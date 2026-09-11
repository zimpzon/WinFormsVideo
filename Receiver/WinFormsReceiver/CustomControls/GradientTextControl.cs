using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinFormsReceiver
{
    public class GradientTextControl : UserControl
    {
        private Color _topColor = Color.SteelBlue;
        private Color _bottomColor = Color.MidnightBlue;
        private Font _overlayFont = new Font("Segoe UI", 14f, FontStyle.Bold);
        private Color _textColor = Color.White;

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "SteelBlue")]
        public Color TopColor
        {
            get => _topColor;
            set { _topColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "MidnightBlue")]
        public Color BottomColor
        {
            get => _bottomColor;
            set { _bottomColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Sample Text")]
        public string OverlayText { get; set; } = "Sample Text";

        [Category("Appearance")]
        public Font OverlayFont
        {
            get => _overlayFont;
            set { _overlayFont = value; Invalidate(); }
        }

        private bool ShouldSerializeOverlayFont() =>
            !_overlayFont.Equals(new Font("Segoe UI", 14f, FontStyle.Bold));

        private void ResetOverlayFont() =>
            OverlayFont = new Font("Segoe UI", 14f, FontStyle.Bold);

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "White")]
        public Color TextColor
        {
            get => _textColor;
            set { _textColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(150)]
        public int BoxAlpha { get; set; } = 150;

        [Category("Appearance")]
        [DefaultValue(typeof(Point), "20, 20")]
        public Point BoxLocation { get; set; } = new Point(20, 20);

        [Category("Appearance")]
        [DefaultValue(10)]
        public int BoxPadding { get; set; } = 10;

        [Category("Appearance")]
        [DefaultValue(6f)]
        public float BoxCornerRadius { get; set; } = 6f;

        public GradientTextControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.UserPaint
                    | ControlStyles.ResizeRedraw, true);

            Size = new Size(400, 250);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using (var gradientBrush = new LinearGradientBrush(
                ClientRectangle, TopColor, BottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(gradientBrush, ClientRectangle);
            }

            if (!string.IsNullOrEmpty(OverlayText))
            {
                var textSize = g.MeasureString(OverlayText, OverlayFont);
                var boxRect = new RectangleF(
                    BoxLocation.X,
                    BoxLocation.Y,
                    textSize.Width + BoxPadding * 2,
                    textSize.Height + BoxPadding * 2);

                using (var path = RoundedRect(boxRect, BoxCornerRadius))
                using (var boxBrush = new SolidBrush(Color.FromArgb(BoxAlpha, 0, 0, 0)))
                {
                    g.FillPath(boxBrush, path);
                }

                using (var textBrush = new SolidBrush(TextColor))
                {
                    g.DrawString(OverlayText, OverlayFont, textBrush,
                        boxRect.X + BoxPadding, boxRect.Y + BoxPadding);
                }
            }
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}