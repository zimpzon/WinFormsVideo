namespace WinFormsReceiver
{
    public partial class LogForm : Form
    {
        private bool _initialized = false;

        public LogForm()
        {
            InitializeComponent();
        }

        private void EnsureHandleCreated()
        {
            // Hack to ensure handle is created so we can log to it before it is otherwise shown.
            var oldPos = Location;
            Location = new Point(-10000, -10000);
            Show();
            Location = oldPos;
            Hide();
        }

        public void LogMessage(string message, Helpers.LogLevel logLevel = Helpers.LogLevel.Information)
        {
            if (Handle == 0)
                EnsureHandleCreated();

            Invoke(() => Helpers.Log(richTextLog, message, logLevel));
        }

        private void LogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
