namespace WinFormsReceiver
{
    public static class Helpers
    {
        public static string GetAspectRatio(int width, int height)
        {
            int gcd = GCD(width, height);
            return $"{width / gcd}:{height / gcd}";
        }

        // Euclidean algorithm for Greatest Common Divisor
        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

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
