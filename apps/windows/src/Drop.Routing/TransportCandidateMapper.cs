using Drop.Transport;

namespace Drop.Routing;

/// <summary>
/// Converts transport-level candidates into routing candidates.
/// </summary>
public static class TransportCandidateMapper
{
    public static ConnectionCandidate Map(
        TransportCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ConnectionCandidate(
            candidateId: candidate.CandidateId,
            transportId: candidate.TransportId,
            routeKind: ResolveRouteKind(candidate),
            transportKind: ResolveTransportKind(candidate),
            capabilities: MapCapabilities(candidate.Capability),
            availability: CandidateAvailability.Reachable,
            priority: CalculatePriority(candidate.Capability));
    }

    private static RouteKind ResolveRouteKind(
        TransportCandidate candidate)
    {
        return candidate.TransportId switch
        {
            "tcp-lan" => RouteKind.LocalLan,
            _ => RouteKind.RemoteDirect
        };
    }

    private static TransportKind ResolveTransportKind(
        TransportCandidate candidate)
    {
        return candidate.Endpoint switch
        {
            _ => TransportKind.ReliableByteStream
        };
    }

    private static RouteCapabilities MapCapabilities(
        TransportCapability capability)
    {
        RouteCapabilities result =
            RouteCapabilities.Reliable |
            RouteCapabilities.Ordered |
            RouteCapabilities.Bidirectional;

        if (capability.SupportsLargeTransfers)
        {
            result |= RouteCapabilities.SupportsLargeTransfers;
        }

        if (capability.SecurityScore >= 80)
        {
            result |= RouteCapabilities.Encrypted;
        }

        return result;
    }

    private static int CalculatePriority(
        TransportCapability capability)
    {
        return capability.SpeedScore;
    }
}