using Microsoft.Extensions.DependencyInjection;

namespace WinFormsReceiver.Context
{
    public static class WinFormsContext
    {
        private static WinFormsContextImpl? _instance;

        public static IWinFormsContext Instance => _instance ?? throw new Exception($"{nameof(WinFormsContext)} is not initialized");

        public static void Initialize(ServiceCollection services)
        {
            _instance = new WinFormsContextImpl();
            _instance.Initialize(services);
        }
    }
}
