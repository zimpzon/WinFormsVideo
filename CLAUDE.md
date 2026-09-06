# Project Overview

This solution is a demonstration application for **sending and receiving live video streams in C# over a network**.

The solution consists of two WinForms applications and two class libraries:

* `WinFormsSender` — sender UI
* `SenderLib` — sender/backend implementation
* `WinFormsReceiver` — receiver UI
* `ReceiverLib` — receiver/backend implementation

The **WinForms projects are frontends only**. The majority of the application logic must live in the corresponding libraries.

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

If a library API change requires a corresponding WinForms change, make the library change and clearly tell the developer what manual WinForms change is required.

Do not modify the WinForms code yourself.
