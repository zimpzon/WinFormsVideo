namespace WinFormsReceiver.Context
{
    public static class WinFormsContext
    {
        private static WinFormsContextImpl? _instance;

        public static IWinFormsContext Instance => _instance ?? throw new Exception("ApplicationContext is not initialized");

        public static void Initialize()
        {
            _instance = new WinFormsContextImpl();
            _instance.Initialize();
        }
    }
}
