using LibVLCSharp.Shared;

namespace WinFormsReceiver.Context
{
    internal sealed class Video : IVideo
    {
        public MediaPlayer MediaPlayer => _mediaPlayer;

        private readonly MediaPlayer _mediaPlayer;
        private readonly LibVLC _libVLC;

        public Video()
        {
            // Global LibVLC options optimized for low-latency RTSP live streams
            string[] libVlcOptions = new string[]
            {
                "--verbose=0",
                "--avcodec-hw=any",         // Enable HW decoding for fastest frame processing
                "--vout=direct3d11",
                "--clock-jitter=0",         // Do not allow clock drift padding
                "--clock-synchro=0",        // Skip audio/video sync delays
                "--rtsp-tcp",               // Ensure TCP transport
                "--drop-late-frames",       // Skip behind-schedule frames to maintain real-time position
                "--skip-frames"             // Skip processing B-frames if decoding lags
            };

            _libVLC = new LibVLC(libVlcOptions);
            _mediaPlayer = new MediaPlayer(_libVLC);
        }

        public void Play(Uri uri)
        {
            using var media = new Media(_libVLC, uri,
                ":rtsp-udp",
                ":network-caching=0",
                ":live-caching=0");

            _mediaPlayer.Play(media);
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
        }
    }
}
