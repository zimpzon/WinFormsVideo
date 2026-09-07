# CURRENT

## Now: UDP transport (alongside TCP) — DONE

The stream can go over UDP: connectionless, unreliable, **a frame with a missing or out-of-order
fragment is dropped whole** (no retransmit, no reorder buffer). TCP stays the default; pick per
config (`Transport = TransportKind.Udp`) or with the CLIs' `--udp` flag. Verified end-to-end
streaming a real 1080p60 file between `SenderCli --udp` and `ReceiverCli --udp`.

The `IVideoStreamServer` / `IVideoClient` seams meant **zero changes to `PlaybackController`,
`ReceivePipeline`, `FFmpegVideoSource`, `FFmpegVideoDecoder`** — just two new classes + a datagram
protocol + ctor wiring.

### What was built

- **`Protocol/DatagramProtocol.cs`** — datagram wire format. Every datagram: `Magic` + `Version` +
  `DatagramType` (`Subscribe` / `StreamInfo` / `FrameFragment` / `Bye`). `FrameFragment` carries a
  27-byte header (`frameSeq`, `ts`, `flags`, `totalLength`, `fragmentIndex`, `fragmentCount`,
  `fragmentLength`) + ≤1200 payload bytes (MTU-safe). Span-based / zero-alloc. Plus
  `Protocol/TransportKind.cs` (`{ Tcp, Udp }`).
- **`UdpVideoStreamServer : IVideoStreamServer`** — binds a UDP socket; a receive thread handles
  `Subscribe` (also the keepalive) → tracks endpoints in a dict with a 6 s timeout, replies with a
  `StreamInfo` datagram + primes with the last keyframe. `Broadcast` fragments each frame into a
  reused send buffer and `SendTo`s a cached `SocketAddress[]` snapshot — no writer threads, no
  queue, zero per-frame allocation. 4 MB `SendBufferSize`.
- **`UdpVideoClient : IVideoClient`** — `Connect` binds + `Socket.Connect`s the peer (so
  `Send`/`Receive` are endpoint-free / filtered), 4 MB `ReceiveBufferSize`, retries `Subscribe`
  until it gets `StreamInfo`. `TryReadPacket` reassembles fragments into grow-only buffers;
  **drop rules**: fragment older than the current frame → drop; first fragment of a newer frame →
  abandon the current (incomplete) one; frame emitted only when every fragment is present.
  Re-sends `Subscribe` every 2 s as keepalive. Counts abandoned/skipped frames.
- **`IVideoClient.FramesDropped`** (new) — whole frames the transport lost. `TcpVideoClient` → 0;
  `UdpVideoClient` counts them. Folded into `ReceiverStatistics.DroppedFrames` by `ReceivePipeline`.
- Wiring: `SenderConfiguration.Transport` / `ReceiverConfiguration.Transport`; `VideoSender` /
  `VideoReceiver` ctors pick the class. `SenderCli` / `ReceiverCli` accept `--udp`.

### Tests (111 total, 0 skipped)

- `DatagramProtocolTests` (7) — round-trips, bounds, bad-magic rejection.
- `UdpVideoStreamServerTests` (6) — subscribe→streaminfo→reassembly, large-frame fragmentation,
  connect/bye/timeout events, no-op with no subscribers, `Broadcast_DoesNotAllocatePerFrame`.
- `UdpVideoClientTests` (8) — handshake, in-order + out-of-order reassembly, **missing fragment →
  frame dropped, next delivered**, stale fragment dropped, handshake timeout, keepalive,
  `TryReadPacket_DoesNotAllocatePerFrame`.
- `SenderToReceiverEndToEndTests` is now a `[Theory]` over `TransportKind` — real sender→receiver
  over **both** TCP and UDP. All 90 pre-existing TCP tests untouched.

### Verify

```powershell
dotnet build WinFormsVideo.slnx    # 9 projects, 0 warnings
dotnet test  WinFormsVideo.slnx    # SenderLib 74, ReceiverLib 37 — 0 skipped
# manual, two terminals:
SenderCli.exe <video> 9000 --udp
ReceiverCli.exe 127.0.0.1 9000 --udp   # some startup drops, then steady ~60 fps, dropped stays flat
```

## Earlier: zero per-frame allocation — DONE

The hot path no longer allocates GC garbage per frame. Verified by four allocation-guard tests
(`GC.GetAllocatedBytesForCurrentThread` deltas, and a snapshot-rebuild counter for the fan-out).

### What changed

- **`Protocol.BufferUtil.EnsureCapacity(ref byte[], int)`** — grow-only buffer helper, shared.
- **`FFmpegVideoSource`** — reuses one grow-only `_packetBuffer`; `EncodedFrame.Data` points into it
  (was `packet.Data.ToArray()` per frame).
- **`TcpVideoClient`** — reuses one grow-only `_payloadBuffer`; `ReceivedPacket.Data` points into it
  (was `new byte[PayloadLength]` per frame).
- **`FFmpegVideoDecoder`** — dropped `VideoFrameConverter` + the `_bgra` `Frame`. Now calls raw
  `sws_getCachedContext` / `sws_scale` with **reused** `byte*[4]` / `int[4]` plane+stride arrays,
  scaling straight into a **3-buffer ring** of managed arrays (allocated once in `Configure`).
  `VideoFrame.Pixels` borrows a ring slot — valid only for the `FrameReady` callback.
- **`VideoFrame`** — `sealed class` → `readonly struct`.
- **`FrameReady`** — `EventHandler<FrameReadyEventArgs>` → `delegate void VideoFrameHandler(object?, in VideoFrame)`;
  `FrameReadyEventArgs` deleted. No per-frame EventArgs / boxed struct.
- **`TcpVideoStreamServer.Broadcast`** — cached connection snapshot rebuilt only on connect/disconnect
  (was `_connections.ToArray()` per broadcast); `_lastKeyframe` is a grow-only `byte[]` (was `ArrayPool`).
- **`RequireState`** (both `PlaybackController` and `ReceivePipeline`) — `params ReceiverState[]` →
  `params ReadOnlySpan<…>` (stackalloc, no heap). Not per-frame, but free.
- `ReceiverConnection` unchanged — its `ArrayPool<byte>` rent/return is already zero-GC.

### The borrowed-buffer contract (now on all three carriers)

`EncodedFrame.Data` / `ReceivedPacket.Data` / `VideoFrame.Pixels` are **borrowed from a reused
buffer** — valid only until the next producing call (or, for `VideoFrame`, only during the
`FrameReady` callback). The whole pump consumes them synchronously; a UI handler that wants to keep
a frame must copy it (e.g. into a `Bitmap`).

### Tests (90, 0 skipped)

New: `…DoesNotAllocatePerFrame` on `FFmpegVideoSource`, `FFmpegVideoDecoder`, `TcpVideoClient`; and
`TcpVideoStreamServerTests.Broadcast_DoesNotRebuildTheConnectionSnapshotPerFrame`. Existing tests
assert metadata only / consume synchronously → unchanged behaviour; the `FrameReady` subscriptions
switched to `(object? _, in VideoFrame f) => …`.

### Verify

```powershell
dotnet build WinFormsVideo.slnx    # 7 projects, 0 warnings
dotnet test  WinFormsVideo.slnx    # SenderLib 62, ReceiverLib 28 — 0 skipped
```

## Earlier: `Sender/SenderCli` + `Receiver/ReceiverCli` — reference console apps for the WinForms port

Both ~55 lines, top-level statements, one `Program.cs`, `<PlatformTarget>x64</PlatformTarget>`.

- **`SenderCli <video-file> [port] [listen-address]`** — `new VideoSender` → subscribe events → `Open` →
  `Start` → print a status line each second from `sender.Statistics`/`State`/`Duration` until EOS/Ctrl+C.
- **`ReceiverCli [sender-address] [port]`** — `new VideoReceiver` → subscribe events (count frames in
  `FrameReady`, where a real UI would copy `Pixels` into a `Bitmap`) → `Connect` → print
  `receiver.Statistics`/`State`/`VideoInfo` each second until Stopped/Faulted/Ctrl+C.

**Verified together**: `SenderCli` streaming a real 1080p60 H.264 file, `ReceiverCli` connected — the
receiver reports `1920x1080 h264`, `net`/`decoded` ~60 fps, 0 dropped; the sender logs
`[receiver] connected …` / `rx=1` and keeps streaming after the receiver leaves (`rx=0`). The WinForms
apps are the same flows behind forms.

## Earlier: the streaming pipeline is functionally complete

Both libraries have **no stubs left**. A `VideoSender` opens a file and streams it over TCP; a
`VideoReceiver` connects, decodes (`ReceivePipeline` — background pump, drop-only pacing, pause =
freeze / resume = jump-to-live, rolling stats), and raises BGRA frames — proven by
`SenderToReceiverEndToEndTests` running the real sender against the real receiver over loopback.

## Next (not started) — this is now WinForms / integration work

1. **Developer: wire the WinForms apps.** Add `ProjectReference` from `WinFormsSender` → `SenderLib`
   and `WinFormsReceiver` → `ReceiverLib` (+ `Protocol`). Both apps must run **x64** (native FFmpeg).
   `Sender/SenderCli/Program.cs` and `Receiver/ReceiverCli/Program.cs` are the reference flows.
   Build the two UIs against the APIs:
   - Sender: file picker, address/port, Start/Pause/Resume/Stop/Restart, a seek slider, a stats panel.
   - Receiver: address/port, Connect/Disconnect, Pause/Resume, a stats panel, and a control that
     paints `VideoFrame` (BGRA32, stride = width*4). `FrameReady` is `VideoFrameHandler`
     (`(object? s, in VideoFrame f)`), raised on a background thread; `f.Pixels` is **borrowed** —
     copy it into a `Bitmap` *inside the handler* (e.g. `new Bitmap(w,h,stride,Format32bppArgb, ptr)`
     from a pinned span, then `bmp.Clone()` or blit) before it returns, then marshal the Bitmap to
     the UI thread.
2. Optional cleanups: extract `IPlaybackClock`/`PlaybackClock` to a shared project; add a
   `dotnet run` smoke of the two apps together; UDP transport behind `IVideoStreamServer`.
