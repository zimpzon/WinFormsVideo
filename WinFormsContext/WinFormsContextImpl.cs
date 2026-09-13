using Microsoft.Extensions.DependencyInjection;

namespace WinFormsReceiver.Context
{
    internal class WinFormsContextImpl : IWinFormsContext
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        public void Initialize()
        {
            // Totally unnecessary to use dependency injection, but just a test of separating BL from UI.
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IVideo, Video>();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }
    }
}
