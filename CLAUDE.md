# CLAUDE.md

This solution simulates drone live video. It has sender code that read a local mp4 file and exposes it via a stream. There is a WinForms project that actually uses the stream and shows live video. Stream may be closed and opened at will and will continue in the sender. IMPORTANT: The WinForms project will use a built-in LibVclSharp WinForms control to display the stream. I expect it to have an RTSP stream to connect to. Directly, no wrapping code, abstractions, or anything else. I will take care of that part.

The sender uses ffmpeg and streams via H.264 over RTSP.

Do not change the WinForms project.
Work exclusively in the current directory.
Do not look for any mp4 files, work with you got locally.
Never go to any parent directories, stay inside workspace.
Keep changes minimal and don't do complicated abstractions.
Do not use git, it has few but large commit. It has no valuable information for you.
Just do smaller operations without asking, I don't want to reply all the time.

You may write current progress in CURRENT.md but KEEP IT SHORT! Hundreds of lines is not allowed!

Ask me if any questions and write clarifications that be useful long-term in CLARIFICATIONS.md
