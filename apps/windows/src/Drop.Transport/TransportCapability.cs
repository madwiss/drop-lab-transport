namespace Drop.Transport;

/// <summary>
/// Describes the capabilities and characteristics of a transport.
/// Scores use a 0-100 range where higher is better.
/// </summary>
public sealed record TransportCapability
{
    public TransportCapability(
        string transportId,
        int speedScore,
        int rangeScore,
        int securityScore,
        int batteryCostScore,
        bool requiresNetwork,
        bool supportsLargeTransfers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportId);

        ValidateScore(speedScore, nameof(speedScore));
        ValidateScore(rangeScore, nameof(rangeScore));
        ValidateScore(securityScore, nameof(securityScore));
        ValidateScore(batteryCostScore, nameof(batteryCostScore));

        TransportId = transportId;
        SpeedScore = speedScore;
        RangeScore = rangeScore;
        SecurityScore = securityScore;
        BatteryCostScore = batteryCostScore;
        RequiresNetwork = requiresNetwork;
        SupportsLargeTransfers = supportsLargeTransfers;
    }

    public string TransportId { get; }

    public int SpeedScore { get; }

    public int RangeScore { get; }

    public int SecurityScore { get; }

    public int BatteryCostScore { get; }

    public bool RequiresNetwork { get; }

    public bool SupportsLargeTransfers { get; }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}