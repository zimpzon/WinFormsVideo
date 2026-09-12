using System.Drawing;

namespace WinFormsReceiver.Context
{
    public interface IVideo
    {
        VideoStats Stats { get; }
        Action<Bitmap>? FrameReady { get; set; }
        void Play(Uri uri);
        void Stop();
    }
}
