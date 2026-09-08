namespace WinFormsReceiver
{
    public partial class LogForm : Form
    {
        public LogForm()
        {
            InitializeComponent();
        }

        public void LogMessage(string message, Helpers.LogLevel logLevel = Helpers.LogLevel.Information)
        {
            Helpers.Log(richTextLog, message, logLevel);
        }
    }
}
