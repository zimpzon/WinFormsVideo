using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    internal static class Program
    {
        public static readonly ApplicationContext Context = new();

        [STAThread]
        static void Main()
        {
            WinFormsContext.Initialize();

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
