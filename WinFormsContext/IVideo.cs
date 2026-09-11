using LibVLCSharp.Shared;

namespace WinFormsReceiver.Context
{
    public interface IVideo
    {
        MediaPlayer MediaPlayer { get; }
        void Play(Uri uri);
        void Stop();
    }
}
