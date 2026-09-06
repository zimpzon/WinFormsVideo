namespace SenderLib;

/// <summary>
/// TCP implementation of <see cref="IVideoStreamServer"/>: listens on the configured
/// endpoint, tracks connected <see cref="ReceiverConnection"/>s, and fans out frames
/// using <see cref="StreamProtocol"/>. Runs independently of playback.
/// </summary>
internal sealed class TcpVideoStreamServer : IVideoStreamServer
{
    private readonly SenderConfiguration _configuration;

    public TcpVideoStreamServer(SenderConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int ReceiverCount => throw new NotImplementedException();

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    public void Start() => throw new NotImplementedException();

    public void Stop() => throw new NotImplementedException();

    public void Broadcast(in EncodedFrame frame) => throw new NotImplementedException();

    public void Dispose()
    {
        // TODO: stop the listener and close all receiver sockets.
    }
}
