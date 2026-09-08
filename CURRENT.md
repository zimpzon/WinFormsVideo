# CURRENT

## Now: TCP transport removed — UDP-only — DONE

The solution is **UDP-only**. TCP was the default/"reliable" path; it's gone because the next step
is *simulated* network latency and packet loss layered on the UDP path, which makes the real TCP
code dead weight. The `IVideoStreamServer` / `IVideoClient` seams already isolated the transport, so
this was a deletion — `PlaybackController`, `ReceivePipeline`, `FFmpegVideoSource`,
`FFmpegVideoDecoder`, `FramePacing`, `VideoSender`, `VideoReceiver` are unchanged in behaviour.

### Deleted

- `Protocol/StreamProtocol.cs` (TCP handshake + `FrameHeader`), `Protocol/TransportKind.cs`.
- `SenderLib/TcpVideoStreamServer.cs`, `SenderLib/ReceiverConnection.cs` (the per-receiver
  writer/reader threads + bounded drop-oldest queue — the trickiest concurrency in the repo).
- `ReceiverLib/TcpVideoClient.cs`.
- Tests: `TcpVideoStreamServerTests`, `TcpTestIo`, `StreamProtocolTests` (Sender);
  `TcpVideoClientTests`, `ProtocolTestServer` (Receiver).

### Kept / moved

- `StreamInfo` + `FrameFlags` → **`Protocol/StreamInfo.cs`** (used by the UDP path too).
- The three shared wire constants `Magic` / `Version` / `MaxPayloadLength` → now members of
  **`DatagramProtocol`**.
- `IVideoStreamServer` / `IVideoClient` interfaces stay (the DI/test seam; one implementation each).
- `SenderConfiguration` / `ReceiverConfiguration` lost their `Transport` property; `VideoSender` /
  `VideoReceiver` ctors hard-wire `UdpVideoStreamServer` / `UdpVideoClient`. Both CLIs lost `--udp`.

### Tests: SenderLib 56, ReceiverLib 38 — 0 skipped

`SenderToReceiverEndToEndTests` is a plain `[Fact]` again (was a `[Theory]` over both transports).
New `StreamInfoTests` (5) covers the `StreamInfo` serialization edge cases that
`StreamProtocolTests` used to. `dotnet build WinFormsVideo.slnx` → 0 warnings.

### Also: control methods are idempotent no-ops

`VideoSender` (Open/Start/Pause/Resume/Stop/Restart/Seek/Close) and `VideoReceiver`
(Connect/Disconnect/Pause/Resume) no longer raise a `ConfigurationError` when called in a state
that doesn't allow the transition — they silently do nothing. Safe to call repeatedly / in any
order / from wired-up UI buttons without state guards. Genuine runtime errors (bad file, socket,
decode) still surface via `ErrorOccurred`. `PlaybackController.RequireState` → `InState` (no side
effect); `ReceivePipeline` gained early-return guards in `Connect`/`Disconnect`/`Pause`/`Resume`.
Guarded by `ControlMethods_AreSafeToCallRepeatedlyAndInAnyOrder_WithoutErrors` in both test suites.

### Also: `ReceiverLib.WinForms` — cached `Bitmap` bridge

New project `Receiver/ReceiverLib.WinForms` (`net10.0-windows`, `UseWindowsForms`) keeps
`System.Drawing` out of the portable core. One public type:

```csharp
var view = new WinFormsFrameView(receiver);   // dispose before the receiver

receiver.FrameReady += (_, in VideoFrame _) => BeginInvoke(() => panel.Invalidate());

protected override void OnPaint(PaintEventArgs e)
{
    if (view.TryGetBitmap(out Bitmap bmp))
        e.Graphics.DrawImage(bmp, ClientRectangle);   // or: view.Paint(e.Graphics, ClientRectangle);
}
```

It wraps each `FrameBufferPool` buffer in a reused `Format32bppPArgb` `Bitmap` once (rebuilds on a
resolution change), maps `TryAcquireFrame`'s `BufferIndex` to the matching cached bitmap, and
returns the last-good bitmap when nothing new decoded. Zero per-frame copy/alloc, no locking in the
bridge (the pool handles the hand-off). Tests: `ReceiverLib.WinForms.Tests` (6).

**Developer step (manual):** add a `ProjectReference` from `WinFormsReceiver` to
`Receiver/ReceiverLib.WinForms/ReceiverLib.WinForms.csproj` (it transitively brings `ReceiverLib`
+ `Protocol`). The app already runs `net10.0-windows` so no TFM change.

### Next

Simulated network conditions on the UDP path (configurable latency + loss), for the demo.

## Earlier this session: zero-copy presentation path in ReceiverLib — DONE

The WinForms receiver will draw frames with a single `Graphics.DrawImage` and **no per-frame copy**.
For that the decoder's output buffers must be (a) stable in memory so the UI can wrap each one in a
GDI+ `Bitmap` exactly once, and (b) safe to draw while the decoder keeps running. So `ReceiverLib`
grows a **triple-buffer swap chain** that the decoder scales directly into and the UI pulls from.

### API added (all in `ReceiverLib`)

- **`FrameBufferPool`** (public) — the rotating pool, sized to the stream (`Width`/`Height`/`Stride`,
  packed BGRA32). `Count` buffers (3), each at a stable `BufferAddress(i)`. Consumer side:
  `bool TryAcquireFrame(out RentedFrame)` — hands out the newest decoded frame; `false` when nothing
  new since the last call. Counters: `PresentedCount`, `SupersededCount`. `Generation` bumps only
  when the buffers are reallocated (resolution change) — the UI rebuilds its wrappers then.
  Internally: producer owns `_writeSlot`, consumer owns `_displaySlot`, `_pendingSlot` is the
  hand-off; only index swaps are locked, pixels are never copied or locked; the decoder never blocks
  on the UI (a slow UI just means decoded frames are superseded).
- **`RentedFrame`** (public readonly struct) — `BufferIndex` (which pool buffer to draw),
  `SequenceNumber`, `Timestamp`.
- **`FrameTarget`** (internal readonly struct) — `{ Scan0, Stride }`, the decoder's write destination.
- **`IVideoDecoder.OutputBuffers`** — `FrameBufferPool?`, null until `Configure`.
- **`VideoReceiver.FrameBuffers`** / **`VideoReceiver.TryAcquireFrame(out RentedFrame)`** — the UI's
  entry points. `FrameBuffers` is non-null from the `Buffering` state onward.
- **`ReceiverStatistics.PresentedFps`** — frames actually handed to the UI per second.

### Changed

- **`FFmpegVideoDecoder`** — the private 3-array ring becomes a `FrameBufferPool`. `sws_scale` writes
  straight into `pool.CurrentWriteTarget()`, then `pool.Commit(seq, ts)`. `TryDecode` still returns
  the borrowed `VideoFrame` (now a view over the just-committed pool buffer) so the legacy
  `FrameReady` path and `ReceiverCli` are unchanged. Pool is kept across a same-resolution
  reconfigure; replaced (Generation++) on a resolution change; released on `Dispose`.
- **`ReceivePipeline`** — exposes `FrameBuffers` / `TryAcquireFrame`; `EmitStatistics` fills
  `PresentedFps` from `pool.PresentedCount`. Pacing/`FrameReady`/state logic unchanged (the
  MaxLateness gate still gates the push event; the swap chain is the real drop mechanism for the
  pull path).

### The developer's WinForms side (not done here)

1. On `StateChanged` into `Playing` (or when `FrameBuffers.Generation` changes): build `Count`
   `Bitmap`s, `new Bitmap(pool.Width, pool.Height, pool.Stride, PixelFormat.Format32bppRgb,
   pool.BufferAddress(i))`. Dispose the old ones first. Dispose all of them before disposing the
   `VideoReceiver` — the addresses are only valid while it lives.
2. `FrameReady` handler → `BeginInvoke(() => panel.Invalidate())` (just the invalidate).
3. `OnPaint`: `if (receiver.TryAcquireFrame(out var f)) _current = _wrappers[f.BufferIndex];`
   then `if (_current != null) e.Graphics.DrawImage(_current, dest);` — one draw call, zero copy.
4. `DoubleBuffered = true` on the panel for flicker-free compositing.

### Verify

```powershell
dotnet build WinFormsVideo.slnx
dotnet test  Receiver/ReceiverLib.Tests/ReceiverLib.Tests.csproj
dotnet test  Sender/SenderLib.Tests/SenderLib.Tests.csproj
```

## Earlier: UDP transport (alongside TCP) — DONE

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
     paints frames via the zero-copy `FrameBuffers` / `TryAcquireFrame` path (see "Now" above).
2. Optional cleanups: extract `IPlaybackClock`/`PlaybackClock` to a shared project; add a
   `dotnet run` smoke of the two apps together.
