using ReceiverLib;

namespace WinFormsReceiver
{
    public class ApplicationContext
    {
        public ApplicationContext()
        {
            VideoReceiver = new VideoReceiver(new ReceiverConfiguration
            {
                SenderAddress = "127.0.0.1",
                SenderPort = 9000
            });
        }

        public readonly VideoReceiver VideoReceiver;
    }
}
