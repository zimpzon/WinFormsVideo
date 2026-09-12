# CLAUDE.md

This solution simulates drone live video. It has sender code that read a local mp4 file and exposes it via a stream. There is a WinForms project that actually uses the stream and shows live video. Stream may be closed and opened at will and will continue in the sender.

The sender uses ffmpeg + meadiax and streams H.264 over RTSP.
The receiver will use ffpmeg nugets (not ffmpeg.exe directly) and IMPORTANTLY must be able to draw in a bitmap before presenting it. I need to draw overlaying lines, text and bitmaps over the video.

Work exclusively in the current directory.
Do NOT look for any mp4 files. You must work in the current directory ONLY.
Never go to any parent directories, stay inside workspace.
Keep changes minimal and don't do complicated abstractions.
DO NOT start inspecting binary compability. You change code and add/remove nugets. You do NOT look at nuget binaries or C# binaries. Expect it to match and I will take care of the rest.

Do not use git. It has no valuable information for you.
Just do smaller operations without asking, I don't want to reply all the time.

WinForms will consume the stream in the WinFormsContext project as much as possible.
Use ffmpeg for decoding.
Only change WinFormsContext. Do NOT change WinForms project.

Decoded video will be shown in the custom control VideoPanelControl.
