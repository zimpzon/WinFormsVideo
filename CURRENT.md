# Current progress

- LibVLCSharp fully removed (NuGet refs, `using`s, MediaPlayer property). No trace left.
- WinFormsContext.Video decodes **in-process** via the `Sdcb.FFmpeg` NuGet package
  (7.0.0 + `Sdcb.FFmpeg.runtime.windows-x64` 7.0.0, pinned together) — no external
  `ffmpeg.exe` process for the receiver.
  - `FormatContext.OpenInputUrl` opens the RTSP URL, finds the video stream, opens a
    `CodecContext` decoder for it.
  - Decode loop: `ReadPackets` → `DecodePacket` (yields native yuv420p `Frame`s) →
    `VideoFrameConverter.ConvertFrame` to Bgra → row-copied into a reused pinned
    buffer → wrapped as a `Bitmap` (`Format32bppRgb`) → `IVideo.FrameReady`.
- `WinFormsReceiver.csproj` now sets `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`
  (`SelfContained=false`) — required for NuGet to copy the native `runtimes/win-x64/native/*.dll`
  ffmpeg binaries into the output folder; confirmed present after build.
- `VideoPanelControl.cs` (unchanged) still draws the overlay (timestamp) directly onto
  the frame bitmap before displaying it.
- Solution builds clean (`dotnet build WinFormsVideo.slnx`).
- **Verified end-to-end**: launched the real app against the running sender
  (`rtsp://127.0.0.1:8554/live`), clicked Play, confirmed via window capture that
  `VideoPanelControl` renders live decoded frames (checked two captures differ, i.e.
  not a frozen frame). Found and fixed one real bug along the way: `bgraFrame` had no
  allocated pixel buffer before `VideoFrameConverter.ConvertFrame` wrote into it
  (`sws_scale`: "bad dst image pointers") — fixed with `Unref()` + `EnsureBuffer()`
  before each conversion. No WinForms-project changes were needed this pass; the
  existing `VideoPanelControl`/`MainForm` wiring from the previous pass was already correct.
