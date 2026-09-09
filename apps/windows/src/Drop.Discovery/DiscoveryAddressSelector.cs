using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Drop.Discovery;

internal readonly record struct LocalUnicastAddress(IPAddress Address, int PrefixLength);

internal static class DiscoveryAddressSelector
{
    public static IPAddress Select(
        IEnumerable<IPAddress> candidates,
        IReadOnlyCollection<LocalUnicastAddress> localAddresses) =>
        candidates
            .Distinct()
            .OrderBy(address => Rank(address, localAddresses))
            .ThenBy(address => Convert.ToHexString(address.GetAddressBytes()), StringComparer.Ordinal)
            .ThenBy(address => address.AddressFamily == AddressFamily.InterNetworkV6 ? address.ScopeId : 0)
            .First();

    public static IReadOnlyList<LocalUnicastAddress> GetLocalAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Where(address => address.Address.AddressFamily is
                    AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Where(address => !IPAddress.IsLoopback(address.Address))
                .Select(address => new LocalUnicastAddress(address.Address, address.PrefixLength))
                .Distinct()
                .ToArray();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    private static int Rank(IPAddress address, IReadOnlyCollection<LocalUnicastAddress> localAddresses)
    {
        if (localAddresses.Any(local => local.Address.Equals(address))) return 6;

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork when IsOnLocalSubnet(address, localAddresses) => 0,
            AddressFamily.InterNetwork when IsPrivateIpv4(address) => 1,
            AddressFamily.InterNetwork when !IPAddress.IsLoopback(address) => 2,
            AddressFamily.InterNetworkV6 when !IPAddress.IsLoopback(address) => 3,
            AddressFamily.InterNetwork => 4,
            _ => 5
        };
    }

    private static bool IsOnLocalSubnet(
        IPAddress candidate,
        IReadOnlyCollection<LocalUnicastAddress> localAddresses) =>
        localAddresses.Any(local =>
            local.Address.AddressFamily == AddressFamily.InterNetwork &&
            local.PrefixLength is > 0 and <= 32 &&
            SharesPrefix(candidate, local.Address, local.PrefixLength));

    private static bool SharesPrefix(IPAddress left, IPAddress right, int prefixLength)
    {
        byte[] leftBytes = left.GetAddressBytes();
        byte[] rightBytes = right.GetAddressBytes();
        int wholeBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int index = 0; index < wholeBytes; index++)
        {
            if (leftBytes[index] != rightBytes[index]) return false;
        }

        if (remainingBits == 0) return true;
        int mask = 0xff << (8 - remainingBits);
        return (leftBytes[wholeBytes] & mask) == (rightBytes[wholeBytes] & mask);
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
