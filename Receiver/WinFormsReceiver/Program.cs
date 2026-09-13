using Microsoft.Extensions.DependencyInjection;
using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Totally unnecessary to use dependency injection, but just a test of separating BL from UI.
            var services = new ServiceCollection();
            services.AddSingleton<IVideo, Video>();

            WinFormsContext.Initialize(services);

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
