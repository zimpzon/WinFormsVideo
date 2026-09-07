using System.Net;
using System.Net.Sockets;
using Protocol;

namespace ReceiverLib.Tests;

/// <summary>
/// Minimal loopback server that speaks <see cref="StreamProtocol"/>, for exercising
/// <see cref="ReceiverLib.TcpVideoClient"/> without pulling in the sender library.
/// </summary>
internal sealed class ProtocolTestServer : IDisposable
{
    private readonly TcpListener _listener;
    private Socket? _client;

    public ProtocolTestServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The <see cref="StreamInfo"/> sent in the standard handshake.</summary>
    public static StreamInfo DefaultStreamInfo { get; } =
        new(codecId: 27, width: 320, height: 240, extradata: new byte[] { 0xAA, 0xBB, 0xCC });

    /// <summary>Accept one connection and write the standard handshake.</summary>
    public void AcceptAndHandshake() => Accept(handshake: null);

    /// <summary>Accept one connection and write <paramref name="handshake"/> verbatim (for bad-handshake tests).</summary>
    public void AcceptAndSend(byte[] handshake) => Accept(handshake);

    public void SendFrame(long sequence, TimeSpan timestamp, bool isKeyFrame, byte[] payload)
    {
        var header = new FrameHeader(
            sequence,
            timestamp.Ticks,
            isKeyFrame ? FrameFlags.KeyFrame : FrameFlags.None,
            payload.Length);

        var buffer = new byte[StreamProtocol.HeaderSize];
        header.TryWrite(buffer);
        _client!.Send(buffer);
        _client!.Send(payload);
    }

    public void CloseClient() => _client?.Close();

    public void Dispose()
    {
        try { _client?.Dispose(); } catch { /* ignore */ }
        _listener.Stop();
    }

    private void Accept(byte[]? handshake)
    {
        _client = _listener.AcceptSocket();
        _client.NoDelay = true;

        if (handshake is not null)
        {
            _client.Send(handshake);
            return;
        }

        var buffer = new byte[StreamProtocol.HandshakeSize(DefaultStreamInfo)];
        StreamProtocol.TryWriteHandshake(buffer, DefaultStreamInfo);
        _client.Send(buffer);
    }
}
