# Clarifications

- FFmpeg NuGet package: used `Sdcb.FFmpeg` (in-process), not an external `ffmpeg.exe`
  process — confirmed by the user. Version 7.0.0, matching the runtime package
  (`Sdcb.FFmpeg.runtime.windows-x64` also 7.0.0 — the bindings package tops out there
  even though NuGet lists a 7.1.0 runtime; keep both pinned equal).
- Sdcb.FFmpeg API calls were resolved from the package's shipped XML doc file
  (`~/.nuget/packages/sdcb.ffmpeg/<version>/lib/<tfm>/Sdcb.FFmpeg.xml`) plus fixing up
  the few remaining gaps (e.g. `Frame.Data`/`Linesize` being 8-element arrays, not the
  4-element ones `ImageUtils.CopyToBuffer` wants) via the compiler's own error output —
  no separate scratch project needed.
- `WinFormsReceiver.csproj` needs `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` (not
  just `<PlatformTarget>x64</PlatformTarget>`) for NuGet to actually copy the
  RID-specific native ffmpeg DLLs into the build output.
