using Microsoft.Extensions.DependencyInjection;

namespace WinFormsReceiver.Context
{
    internal class WinFormsContextImpl : IWinFormsContext
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        public void Initialize(ServiceCollection services)
        {
            ServiceProvider = services.BuildServiceProvider();
        }
    }
}
