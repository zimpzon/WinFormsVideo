namespace WinFormsReceiver.Context
{
    internal class WinFormsContextImpl : IWinFormsContext
    {
        public IVideo Video { get; private set; } = null!;

        public void Initialize()
        {
            Video = new Video();
        }
    }
}
