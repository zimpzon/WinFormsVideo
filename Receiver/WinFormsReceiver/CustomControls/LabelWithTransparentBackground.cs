namespace WinFormsReceiver.CustomControls
{
    public class LabelWithTransparentBackground : Control
    {
        public LabelWithTransparentBackground()
        {
            //SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            //SetStyle(ControlStyles.UserPaint, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Do not paint background to make it transparent
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //using Brush b = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
            //e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, this.Size));
            e.Graphics.DrawString("hello from control", Font, Brushes.White, new PointF(0, 0));
        }
    }
}
