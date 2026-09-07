using Protocol;

namespace ReceiverLib;

/// <summary>
/// Decodes compressed <see cref="ReceivedPacket"/>s into displayable <see cref="VideoFrame"/>s.
/// The concrete FFmpeg implementation is an internal detail.
/// </summary>
public interface IVideoDecoder : IDisposable
{
    /// <summary>Codec short name (e.g. "h264"). Valid after <see cref="Configure"/>.</summary>
    string CodecName { get; }

    /// <summary>Prepare the decoder from the handshake's <see cref="StreamInfo"/>. Called once.</summary>
    void Configure(StreamInfo info);

    /// <summary>
    /// Feed one packet. Returns true and a frame when the decoder produced output for this packet.
    /// The returned <see cref="VideoFrame.Pixels"/> is borrowed and only valid until the next
    /// <see cref="TryDecode"/> call.
    /// </summary>
    bool TryDecode(in ReceivedPacket packet, out VideoFrame frame);

    /// <summary>Drop any buffered state (e.g. after a gap or seek).</summary>
    void Flush();
}
