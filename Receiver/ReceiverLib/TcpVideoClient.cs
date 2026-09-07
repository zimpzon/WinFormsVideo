using System.Net.Sockets;
using Protocol;

namespace ReceiverLib;

/// <summary>
/// TCP implementation of <see cref="IVideoClient"/>: connects to the sender, validates the
/// <see cref="StreamProtocol"/> handshake and reads the framed packet stream. Reads are pull-based
/// and blocking — the pipeline calls <see cref="TryReadPacket"/> on its own background thread.
/// </summary>
internal sealed class TcpVideoClient : IVideoClient
{
    private readonly ReceiverConfiguration _configuration;
    private readonly object _gate = new();
    private readonly byte[] _headerBuffer = new byte[StreamProtocol.HeaderSize];

    // Reused across frames — ReceivedPacket.Data points into this until the next read.
    private byte[] _payloadBuffer = Array.Empty<byte>();

    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _disconnectRaised;

    public TcpVideoClient(ReceiverConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>The stream description read from the handshake. Null until <see cref="Connect"/> succeeds.</summary>
    public StreamInfo? StreamInfo { get; private set; }

    /// <summary>TCP is reliable — no frames are ever lost in transit.</summary>
    public long FramesDropped => 0;

    public event EventHandler? Connected;

    public event EventHandler? Disconnected;

    public void Connect()
    {
        lock (_gate)
        {
            if (_client is not null)
            {
                return;
            }

            var client = new TcpClient { NoDelay = true };
            try
            {
                client.Connect(_configuration.SenderAddress, _configuration.SenderPort);
                NetworkStream stream = client.GetStream();
                StreamInfo streamInfo = ReadHandshake(stream);

                _client = client;
                _stream = stream;
                _disconnectRaised = 0;
                StreamInfo = streamInfo;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        Connected?.Invoke(this, EventArgs.Empty);
    }

    public bool TryReadPacket(out ReceivedPacket packet)
    {
        packet = default;
        NetworkStream? stream = _stream;
        if (stream is null)
        {
            return false;
        }

        try
        {
            if (!TryReadFull(stream, _headerBuffer))
            {
                MarkDisconnected();
                return false;
            }

            if (!FrameHeader.TryRead(_headerBuffer, out FrameHeader header))
            {
                throw new InvalidDataException("Malformed frame header on the wire.");
            }

            int length = header.PayloadLength;
            BufferUtil.EnsureCapacity(ref _payloadBuffer, length);
            stream.ReadExactly(_payloadBuffer.AsSpan(0, length));

            packet = new ReceivedPacket(
                header.SequenceNumber,
                TimeSpan.FromTicks(header.TimestampTicks),
                header.IsKeyFrame,
                _payloadBuffer.AsMemory(0, length));
            return true;
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or EndOfStreamException)
        {
            MarkDisconnected();
            return false;
        }
    }

    public void Disconnect()
    {
        lock (_gate)
        {
            _stream = null;
            _client?.Dispose();
            _client = null;
        }

        MarkDisconnected();
    }

    public void Dispose() => Disconnect();

    private static StreamInfo ReadHandshake(Stream stream)
    {
        var prefix = new byte[StreamProtocol.HandshakePrefixSize];
        stream.ReadExactly(prefix);
        if (!StreamProtocol.TryReadHandshakeExtradataLength(prefix, out int extradataLength))
        {
            throw new InvalidDataException("Sender handshake was not recognised.");
        }

        var full = new byte[StreamProtocol.HandshakePrefixSize + extradataLength];
        prefix.CopyTo(full, 0);
        if (extradataLength > 0)
        {
            stream.ReadExactly(full.AsSpan(StreamProtocol.HandshakePrefixSize));
        }

        if (!StreamProtocol.TryReadHandshake(full, out byte version, out StreamInfo info) ||
            version != StreamProtocol.Version)
        {
            throw new InvalidDataException($"Unsupported sender protocol (version {version}).");
        }

        return info;
    }

    private void MarkDisconnected()
    {
        if (Interlocked.Exchange(ref _disconnectRaised, 1) == 0)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Fill <paramref name="buffer"/>. Returns false only on a clean EOF at a frame boundary.</summary>
    private static bool TryReadFull(Stream stream, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = stream.Read(buffer[read..]);
            if (n == 0)
            {
                if (read == 0)
                {
                    return false;
                }

                throw new EndOfStreamException("Connection closed mid-frame.");
            }

            read += n;
        }

        return true;
    }
}
