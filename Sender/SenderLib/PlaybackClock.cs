namespace SenderLib;

/// <summary>
/// Default <see cref="IPlaybackClock"/>: a monotonic time base (via <see cref="TimeProvider"/>)
/// offset by the current seek position, so the pump can pace frames against their presentation
/// timestamps instead of sending them as fast as they are read.
/// </summary>
internal sealed class PlaybackClock : IPlaybackClock
{
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    private TimeSpan _basePosition;
    private long _runningSince;
    private bool _isRunning;

    public PlaybackClock(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TimeSpan Position
    {
        get
        {
            lock (_gate)
            {
                return _isRunning ? _basePosition + _timeProvider.GetElapsedTime(_runningSince) : _basePosition;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            _runningSince = _timeProvider.GetTimestamp();
            _isRunning = true;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (!_isRunning)
            {
                return;
            }

            _basePosition += _timeProvider.GetElapsedTime(_runningSince);
            _isRunning = false;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_isRunning)
            {
                return;
            }

            _runningSince = _timeProvider.GetTimestamp();
            _isRunning = true;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _basePosition = TimeSpan.Zero;
            _isRunning = false;
        }
    }

    public void SeekTo(TimeSpan position)
    {
        lock (_gate)
        {
            _basePosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            _runningSince = _timeProvider.GetTimestamp();
        }
    }
}
