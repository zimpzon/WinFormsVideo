namespace WinFormsReceiver
{
    public static class Helpers
    {
        public enum LogLevel
        {
            Information,
            Error
        };

        public static void Log(RichTextBox box, string message, LogLevel level = LogLevel.Information)
        {
            string timestamp = $"[{DateTime.Now:HH:mm:ss}] ";

            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            // Timestamp
            box.SelectionColor = Color.Gray;
            box.SelectionFont = box.Font;
            box.AppendText(timestamp);

            // Message
            if (level == LogLevel.Error)
            {
                box.SelectionColor = Color.Red;
                box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            }
            else
            {
                box.SelectionColor = Color.Black;
                box.SelectionFont = box.Font;
            }

            box.AppendText(message + Environment.NewLine);

            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
        }
    }
}
