using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            WinFormsContext.Initialize();

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
