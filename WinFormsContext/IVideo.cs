using System.Drawing;

namespace WinFormsReceiver.Context
{
    public interface IVideo
    {
        TimeSpan TimePlayed { get; }
        Action<Bitmap>? FrameReady { get; set; }
        void Play(Uri uri);
        void Stop();
    }
}
