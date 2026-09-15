using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Totally unnecessary to use dependency injection, but just a test of separating BL from UI.
            var builder = Host.CreateDefaultBuilder();
            using var serviceHost = builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IVideo, Video>();
                services.AddTransient<MainForm>();
                services.AddTransient<LogForm>();
            }).Build();

            var mainForm = serviceHost.Services.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
    }
}
