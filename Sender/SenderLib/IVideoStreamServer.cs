using Protocol;

namespace SenderLib;

/// <summary>
/// Accepts receiver connections and broadcasts encoded frames to them. Runs
/// independently of playback: the pipeline keeps producing frames whether or not
/// any receiver is connected.
/// </summary>
public interface IVideoStreamServer : IDisposable
{
    /// <summary>Number of receivers currently connected.</summary>
    int ReceiverCount { get; }

    /// <summary>Raised after a receiver connects.</summary>
    event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    /// <summary>Raised after a receiver disconnects or is dropped.</summary>
    event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    /// <summary>
    /// Begin listening for receiver connections. <paramref name="streamInfo"/> is sent to each
    /// receiver in the handshake so it can build a decoder.
    /// </summary>
    void Start(StreamInfo streamInfo);

    /// <summary>Stop listening and disconnect all receivers.</summary>
    void Stop();

    /// <summary>Send a frame to every connected receiver. No-op when there are none.</summary>
    void Broadcast(in EncodedFrame frame);
}
