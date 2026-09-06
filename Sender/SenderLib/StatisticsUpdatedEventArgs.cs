namespace SenderLib;

/// <summary>Raised periodically with a fresh <see cref="SenderStatistics"/> snapshot.</summary>
public sealed class StatisticsUpdatedEventArgs : EventArgs
{
    public StatisticsUpdatedEventArgs(SenderStatistics statistics)
    {
        Statistics = statistics;
    }

    public SenderStatistics Statistics { get; }
}
