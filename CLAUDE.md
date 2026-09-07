# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project Overview

This solution is a demonstration application for **sending and receiving live video streams in C# over a network**.

The solution consists of two WinForms applications, two class libraries, and a shared wire-format project:

* `WinFormsSender` — sender UI
* `SenderLib` — sender/backend implementation
* `WinFormsReceiver` — receiver UI
* `ReceiverLib` — receiver/backend implementation
* `Protocol` — the on-the-wire format (`StreamProtocol` / `FrameHeader`), referenced by both libs

The **WinForms projects are frontends only**. The majority of the application logic must live in the corresponding libraries.

---

# Current State (read this first)

`CURRENT.md` has the live task status — read it first. Summary of what's implemented:

* **`SenderLib` orchestration is real** (flat in `Sender/SenderLib/`, namespace `SenderLib`):
  * `VideoSender` — public facade (Open/Start/Pause/Resume/Stop/Restart/Seek/Close + state/stats/error events), thin wrapper over `PlaybackController`.
  * `PlaybackController` (internal) — working state machine + background pump thread driving read→pace→broadcast against the injected `IVideoSource` / `IPlaybackClock` / `IVideoStreamServer`. All `IVideoSource` access after `Open` is on the pump thread; control methods use a command queue. Streams with zero receivers. Raises `StateChanged` / `StatisticsUpdated` / `ErrorOccurred` / `EndOfVideoReached` **on the pump thread** (WinForms marshals).
  * `PlaybackClock` (internal, `IPlaybackClock`) — `TimeProvider`-based monotonic clock; inject a `TimeProvider` for deterministic tests.
  * `FramePacing` (internal, pure) — `Send` / `Wait` / `Drop` decision; keyframes never dropped, non-keyframes past a 200 ms drop threshold discarded.
  * `TcpVideoStreamServer` (internal, `IVideoStreamServer`) + `ReceiverConnection` — **implemented**. `TcpListener` + accept thread; per-receiver writer/reader threads with a bounded (120-frame) drop-oldest-non-keyframe queue and a socket `SendTimeout`; server-wide monotonic sequence numbers; late joiners primed with the most recent keyframe. `Start(StreamInfo)` — the info is written in each receiver's handshake.
  * `FFmpegVideoSource` (internal, `IVideoSource`) — **implemented** via `Sdcb.FFmpeg` 7.0.0 + `Sdcb.FFmpeg.runtime.windows-x64` 7.0.0. A pure demuxer (no decode): opens a container, reads video packets → `EncodedFrame` (PTS, keyframe flag, **copied into a reused grow-only buffer**), seeks to keyframes; `VideoInfo` also carries `CodecId` + `CodecExtradata` (the avcC block). **All FFmpeg / `Sdcb.FFmpeg.Raw` code lives in this one file** and its exceptions never leak (→ `FileNotFoundException` / `NotSupportedException`).
  * DTOs/enums: `SenderConfiguration`, `PlaybackState`, `VideoInfo` (+`CodecId`/`CodecExtradata`), `SenderStatistics`, `SenderError`/`SenderErrorKind`, `EncodedFrame`.
* **`SenderLib` has no stubs left** — the sender pipeline (open file → pace to PTS → TCP fan-out) is functionally complete. No `#pragma`/`NoWarn` suppressions anywhere; clean build.
* **`Protocol`** — the shared wire format (`public`).
  * **TCP** (`StreamProtocol`): handshake = `Magic` "WFV1" (4) + `Version` (1) + `StreamInfo` = `[codecId:4][width:4][height:4][extradataLen:4][extradata]` (LE). Then 24-byte `FrameHeader` + payload per frame.
  * **UDP** (`DatagramProtocol`): every datagram = `Magic` + `Version` + `DatagramType` (`Subscribe`/`StreamInfo`/`FrameFragment`/`Bye`). `FrameFragment` = 27-byte `FragmentHeader` (frameSeq, ts, flags, totalLength, fragmentIndex/Count, fragmentLength) + ≤1200 payload bytes.
  * `TransportKind { Tcp, Udp }`; `BufferUtil.EnsureCapacity(ref byte[], int)` — the grow-only buffer helper both libs use on the per-frame path.
  * **No bitstream filter** — the receiver decodes raw AVCC packets using the handshake extradata (Sdcb.FFmpeg 7.0 has no `av_bsf_*`).
* **Transport is selectable** via `SenderConfiguration.Transport` / `ReceiverConfiguration.Transport` (default `Tcp`). `VideoSender`/`VideoReceiver` ctors pick `TcpVideoStreamServer`+`TcpVideoClient` or `UdpVideoStreamServer`+`UdpVideoClient` — nothing else in the pipeline changes.
  * **UDP** (`UdpVideoStreamServer` / `UdpVideoClient`): connectionless, unicast subscribe + 2 s keepalive + 6 s server-side timeout, MTU-safe fragmentation with reassembly. **A frame with a missing or out-of-order fragment is dropped whole** — no retransmit, no reorder buffer. `IVideoClient.FramesDropped` reports transport losses (0 for TCP), folded into `ReceiverStatistics.DroppedFrames`.
* Sender is **streaming-only** — forwards compressed `EncodedFrame`s (AVCC as stored in the container), does not decode to pixels.
* **`ReceiverLib`** (flat, namespace `ReceiverLib`): `VideoReceiver` facade (Connect/Disconnect/Pause/Resume + state/stats/frame/error events) over `ReceivePipeline` (injectable: `IVideoClient` + `IVideoDecoder` + `IPlaybackClock`).
  * `TcpVideoClient` (internal, `IVideoClient`) — **implemented**. Connects, reads the two-part handshake → exposes `Protocol.StreamInfo?`; pull-style blocking `TryReadPacket` → `ReceivedPacket` (payload **read into a reused grow-only buffer**; returns false on EOF/socket error + raises `Disconnected`; throws only on a malformed header).
  * `FFmpegVideoDecoder` (internal, `IVideoDecoder`, `unsafe`) — **implemented** via Sdcb.FFmpeg. `Configure(StreamInfo)` builds a `CodecContext` (decoder by codec id + `av_malloc`'d `extradata` + open); `TryDecode` feeds the AVCC bytes (borrowed-pointer `Packet` pinned across `SendPacket`), `ReceiveFrame`, then **raw `sws_scale` (reused plane/stride arrays) straight into a 3-buffer ring** → `VideoFrame` (packed BGRA32). **All Sdcb.FFmpeg / native code lives in this one file.** Assumes 1 packet → 1 frame.
  * `ReceivePipeline` (internal) — **implemented**. Background pump: connect → `decoder.Configure` → loop read/decode → drop-only pacing (present as decoded; drop frames >200 ms behind the clock) → `FrameReady`. State machine `Idle→Connecting→Buffering→Playing⇄Paused` + `Stopped`/`Faulted`; `Pause` freezes + discards, `Resume` jumps to live; rolling `ReceiverStatistics`; errors mapped, never escape.
  * `PlaybackClock` (internal, `IPlaybackClock`) — **implemented**, `TimeProvider`-based (a copy of `SenderLib`'s; candidate to share).
  * **`ReceiverLib` has no stubs left.** `ReceiverLib.csproj` has `<AllowUnsafeBlocks>` + `Sdcb.FFmpeg` refs + `InternalsVisibleTo`; no `NoWarn`.
  * DTOs: `ReceiverConfiguration`/`ReceiverState`/`VideoInfo`/`VideoFrame` (a `readonly struct`, +`FramePixelFormat`, WinForms-agnostic)/`ReceivedPacket`/`ReceiverStatistics`/`ReceiverError`. `FrameReady` is `delegate void VideoFrameHandler(object?, in VideoFrame)` — **not** `EventHandler<T>` (no per-frame EventArgs).
* **No per-frame allocation on the hot path.** `EncodedFrame.Data` / `ReceivedPacket.Data` / `VideoFrame.Pixels` are **borrowed from reused buffers** — valid only until the next producing call (or, for `Pixels`, only during the `FrameReady` callback). A consumer that keeps a frame must copy it. Guarded by `*DoesNotAllocatePerFrame` tests.
* **The streaming pipeline is functionally complete** — a `VideoSender` streams a file over TCP and a `VideoReceiver` decodes it to BGRA frames, proven by `SenderToReceiverEndToEndTests`. Remaining work is the two WinForms UIs (developer-owned) + their `ProjectReference`s to the libs.
* Tests: `Sender/SenderLib.Tests` (74) and `Receiver/ReceiverLib.Tests` (37) — **0 skipped**. `Fakes/` for the seams; real loopback sockets (TCP + UDP); `TestVideo`/`DecoderTestData` encode+demux a tiny real mp4 in-memory; `Microsoft.Extensions.TimeProvider.Testing`; `ProtocolTestServer` loopback helper; `*DoesNotAllocatePerFrame` alloc guards; `SenderToReceiverEndToEndTests` is a `[Theory]` over both transports. `ReceiverLib.Tests` also references `SenderLib` for the end-to-end test. Both libs have `<InternalsVisibleTo>` for their test project.
* Still **no project references** from the WinForms apps to the libs. Both libs pull native FFmpeg (win-x64) — the WinForms apps must run x64 once they reference them (AnyCPU on 64-bit Windows already does).
* `Sender/SenderCli` (`SenderCli <video-file> [port] [address]`) and `Receiver/ReceiverCli` (`ReceiverCli [address] [port]`) — small console apps that drive `VideoSender` / `VideoReceiver` and print per-second stats. Reference flows for the two WinForms apps; verified streaming a real 1080p60 file between them.
* `WinFormsVideo.slnx` lists all nine projects; `dotnet build WinFormsVideo.slnx` is clean.
* `WinFormsSender`'s form class was renamed `Form1` -> `MainForm` (one-off developer-approved fix so `Program.cs` compiled). The form is still otherwise the empty default.

Target framework is `net10.0` for the libs/tests and `net10.0-windows` for the WinForms apps. `Nullable` and `ImplicitUsings` are enabled everywhere.

---

# Build, Run & Test

Commands run from repo root:

```powershell
# Build a single library (the usual inner loop for Claude)
dotnet build Sender/SenderLib/SenderLib.csproj
dotnet build Receiver/ReceiverLib/ReceiverLib.csproj

# Tests — xUnit
dotnet test Sender/SenderLib.Tests/SenderLib.Tests.csproj
dotnet test Receiver/ReceiverLib.Tests/ReceiverLib.Tests.csproj
dotnet test Sender/SenderLib.Tests/SenderLib.Tests.csproj --filter "FullyQualifiedName~VideoSenderTests.Dispose"   # single test

# Apps
dotnet run --project Receiver/WinFormsReceiver/WinFormsReceiver.csproj
dotnet run --project Sender/WinFormsSender/WinFormsSender.csproj

# Whole solution
dotnet build WinFormsVideo.slnx
```

Keep the libraries testable: new components should take their collaborators as constructor-injected
interfaces (see `PlaybackController`) so `SenderLib.Tests`/`ReceiverLib.Tests` can substitute fakes.

---

## Critical Development Rule

**Claude must NOT modify the WinForms projects.**

The WinForms UI will be implemented and maintained manually by the developer.

Claude may inspect the WinForms projects when necessary to understand how the UI is intended to interact with the libraries, but Claude must only make code changes inside:

* `SenderLib`
* `ReceiverLib`

Do not modify:

* WinForms forms
* WinForms designer files
* WinForms controls
* WinForms event handlers
* WinForms project files
* WinForms configuration

If a change to a WinForms project appears necessary, **do not make the change**. Instead, explain what the developer needs to change manually.

---

# Architecture

The intended architecture is:

```text
                    ┌─────────────────────┐
                    │   Video File        │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │    SenderLib       │
                    │                     │
                    │ File reading        │
                    │ Video decoding      │
                    │ Playback             │
                    │ Streaming            │
                    │ Statistics           │
                    └──────────┬──────────┘
                               │
                               │ Network
                               ▼
                    ┌─────────────────────┐
                    │    ReceiverLib     │
                    │                     │
                    │ Network receiving   │
                    │ Video decoding      │
                    │ Playback/viewing     │
                    │ Statistics          │
                    └──────────┬──────────┘
                               │
                               ▼
                         Receiver UI
```

The libraries must be designed so that the core functionality is **not dependent on WinForms**.

The libraries should expose clean C# APIs, events, properties, and data types that allow the WinForms applications to display state and video.

---

# WinFormsSender

`WinFormsSender` is the frontend for the sender.

The user should be able to:

* Select a video file.
* Start streaming.
* Pause streaming.
* Resume streaming.
* Restart the video.
* Control playback position.
* Configure the destination IP address.
* Configure the destination port.
* View available playback/streaming statistics.

Possible UI information includes:

* Current playback position.
* Total video duration.
* Playback state.
* Frames processed.
* Frames sent.
* Current FPS.
* Bitrate.
* Connection/listener information.
* Other useful streaming statistics.

The UI should ideally contain a playback-position slider allowing the user to seek within the video.

### Important streaming behavior

The sender must **continue streaming even when there are no receivers/listeners**.

The sender should not require a receiver to be connected before playback/streaming starts.

When a receiver connects later, it should receive the stream from the sender's current playback position according to the implemented streaming protocol.

The sender's backend functionality belongs in `SenderLib`.

---

# WinFormsReceiver

`WinFormsReceiver` is the frontend for the receiver.

The user should be able to:

* Configure the sender/receiving IP address.
* Configure the receiving port.
* Connect to a stream.
* Disconnect from a stream.
* Pause viewing.
* Resume viewing.
* View available statistics.

Possible UI information includes:

* Connection state.
* Current video position if available.
* Received FPS.
* Decoded FPS.
* Dropped frames.
* Network bitrate.
* Latency if measurable.
* Video resolution.
* Codec information.
* Other useful receiver statistics.

The receiver UI must not contain the networking or video-decoding implementation. That functionality belongs in `ReceiverLib`.

---

# SenderLib

`SenderLib` contains all backend functionality required to read a video file and stream it over the network.

It must not depend on WinForms.

Responsibilities include:

* Opening video files.
* Reading video data.
* Decoding/processing video as required.
* Playback timing.
* Streaming video over the network.
* Maintaining playback state.
* Pause/resume.
* Restart.
* Seeking.
* Playback position tracking.
* Stream statistics.
* Handling receivers/listeners.
* Handling errors and connection problems.
* Resource cleanup.

The library should provide a clean public API for the WinForms sender to control playback and streaming.

The library should support at least:

```text
Open video
Start
Pause
Resume
Stop
Restart
Seek
Close
```

The API should be designed so additional functionality can be added later without requiring major architectural changes.

### Sender timing

Video playback must respect the timestamps/frame rate of the source video.

The sender must not simply decode and send frames as fast as possible.

The sender should maintain a playback clock and send frames according to their intended presentation timestamps.

### No-listener behavior

Streaming/playback must continue when there are zero receivers.

The sender must therefore treat the stream production/playback lifecycle independently from individual receiver connections.

---

# ReceiverLib

`ReceiverLib` contains all backend functionality required to receive and display the network video.

It must not depend on WinForms.

Responsibilities include:

* Connecting to the sender.
* Disconnecting.
* Receiving network data.
* Reconstructing the video stream.
* Decoding video.
* Managing received frames.
* Playback/viewing state.
* Pause/resume viewing.
* Frame timing.
* Dropped-frame handling.
* Network statistics.
* Video statistics.
* Error handling.
* Resource cleanup.

The library should provide a clean public API for the WinForms receiver.

The API should support at least:

```text
Connect
Disconnect
Pause
Resume
```

Additional functionality should be added where appropriate.

---

# Video Rendering

The receiver library must not require WinForms controls.

Video frames should be exposed through a library-level abstraction that the WinForms application can consume.

The WinForms application will be responsible for displaying the received frames.

Do not put WinForms-specific rendering code into `ReceiverLib`.

The initial implementation may use a simple frame representation suitable for WinForms rendering, but the architecture should not prevent future migration to a more efficient rendering approach such as GPU-based rendering.

---

# Threading

Video processing, encoding/decoding, and network operations must not block the WinForms UI thread.

The libraries should perform long-running operations asynchronously or on appropriate background threads.

UI-related thread marshaling belongs to the WinForms frontend.

The libraries themselves should not call WinForms APIs such as:

```text
Control.Invoke
Control.BeginInvoke
Application.DoEvents
```

---

# Performance

This is a **real-time video streaming demonstration**, so performance matters.

Avoid unnecessary:

* Memory allocations per frame.
* Copies of complete video frames.
* Blocking operations.
* Unbounded queues.
* Growing buffers that increase latency.

For real-time playback, keeping latency low is more important than displaying every frame.

If the receiver falls behind, the implementation should prefer dropping obsolete frames rather than allowing latency to continuously increase.

Avoid introducing GC pressure in the frame-processing path where reasonably possible.

---

# Separation of Concerns

Keep these responsibilities separate:

```text
Video file handling
        ↓
Video decoding
        ↓
Playback timing
        ↓
Network transport
        ↓
Receiving
        ↓
Video decoding
        ↓
Frame delivery
        ↓
WinForms rendering
```

Do not mix these concerns unnecessarily.

For example:

* `SenderLib` should not know about WinForms controls.
* `ReceiverLib` should not know about WinForms controls.
* WinForms should not implement video decoding.
* WinForms should not implement network protocols.
* WinForms should not contain playback logic.

---

# Public API Design

Prefer simple, explicit APIs.

Use strongly typed classes for:

* Playback state.
* Video information.
* Statistics.
* Video frames.
* Configuration.
* Errors where appropriate.

Prefer events or suitable asynchronous mechanisms for pushing state/frame updates to the UI.

Avoid exposing FFmpeg/native implementation details directly through the public API unless there is a strong reason to do so.

The WinForms applications should be able to use the libraries without needing to understand their internal implementation.

---

# Error Handling

The libraries should handle expected runtime problems gracefully, including:

* Invalid video files.
* Unsupported codecs.
* Network disconnections.
* Receiver disconnections.
* Network errors.
* Corrupt/incomplete video data.
* Invalid configuration.
* End of video.
* Resource cleanup.

Do not crash the application because a receiver disconnects or a network operation fails.

Expose useful errors/status information through the library API so the WinForms UI can display it.

---

# Technology

The project is written in C#/.NET.

FFmpeg may be used for video processing/decoding/encoding where appropriate.

Keep FFmpeg-specific code isolated as much as reasonably possible.

Do not introduce a large framework or dependency when a simple implementation is sufficient for this demonstration.

---

# Development Approach

Implement functionality incrementally.

Prioritize:

1. Open a video file.
2. Decode/read video frames.
3. Implement sender playback timing.
4. Stream the video over the network.
5. Receive the stream.
6. Decode received video.
7. Deliver frames to the receiver UI.
8. Add pause/resume.
9. Add seeking/restart.
10. Add statistics.
11. Improve performance and latency.

Do not attempt to implement the entire system at once.

After each major step, keep the solution buildable and testable.

---

# Important Rule for Claude

**Only modify library code.**

Allowed:

```text
SenderLib/**
ReceiverLib/**
```

Not allowed:

```text
WinFormsSender/**
WinFormsReceiver/**
```

The WinForms projects may be inspected for context, but changes to them must be left to the developer.

If a library API change requires a corresponding WinForms change, make the library change and clearly
tell the developer what manual WinForms change is required.

Do not modify the WinForms code yourself.

NB: CURRENT.md is what we are currently working on, or when completed, what we last worked on. You must
fill in CURRENT.md whenever starting on a task.