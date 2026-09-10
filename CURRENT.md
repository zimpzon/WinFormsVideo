# CURRENT

## Task: pivot to ffmpeg / H.264 over RTSP

CLAUDE.md was rewritten to a much simpler design: sender uses (in-process Sdcb.FFmpeg) to stream
a local mp4 as H.264 over RTSP; ReceiverCli just verifies the stream reads; WinFormsReceiver shows
live video. The old custom UDP/TCP pipeline is being removed.

### Done — dead-code removal pass
- Deleted projects: `Protocol`, `Sender/SenderCli`, `Sender/WinFormsSender`, `Sender/SenderLib.Tests`,
  `Receiver/ReceiverLib.Tests`, `Receiver/ReceiverLib.WinForms`, `Receiver/ReceiverLib.WinForms.Tests`.
- `WinFormsVideo.slnx` trimmed to 4 projects: SenderLib, ReceiverLib, ReceiverCli, WinFormsReceiver.
- SenderLib: removed the custom transport + old `VideoSender` facade + pacing. Kept the reusable
  FFmpeg demux core: `FFmpegVideoSource` / `IVideoSource` / `VideoInfo` / `EncodedFrame` + config/enum DTOs.
- ReceiverLib: removed `VideoReceiver` / `ReceivePipeline` / `UdpVideoClient` / clocks. Kept the
  reusable decode + zero-copy display core: `FFmpegVideoDecoder` / `IVideoDecoder` / `FrameBufferPool`
  / `FrameTarget` / `RentedFrame` / `VideoFrame` / `ReceivedPacket` + DTOs. `IVideoDecoder.Configure`
  now takes `(codecId, width, height, extradata)` instead of the old `StreamInfo`.
- `ReceiverCli/Program.cs` is a placeholder pending the RTSP verifier.
- Builds clean: SenderLib, ReceiverLib, ReceiverCli.

### Broken until the RTSP pass (expected)
- `WinFormsReceiver` does not build — it uses the removed `VideoReceiver` API + `ReceiverLib.WinForms`.
  Not touched (developer-owned). The RTSP pass must restore an equivalent `VideoReceiver` surface
  (or the developer adjusts the form).

### Next: implement RTSP
- Sender: in-process Sdcb.FFmpeg — read mp4 (`FFmpegVideoSource`), encode/mux H.264, publish RTSP
  (server, streams with zero clients, resumable).
- ReceiverLib: RTSP client feeding `FFmpegVideoDecoder` → `FrameBufferPool`; restore `VideoReceiver`.
- ReceiverCli: open the RTSP URL, confirm packets read, print codec/res/fps.
