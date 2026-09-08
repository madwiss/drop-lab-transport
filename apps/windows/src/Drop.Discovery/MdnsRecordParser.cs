using System.Globalization;
using Makaretu.Dns;

namespace Drop.Discovery;

internal static class MdnsRecordParser
{
    public static bool TryParse(
        DomainName instanceName,
        Message message,
        DateTimeOffset observedAtUtc,
        out DnsSdRecord record)
    {
        record = null!;
        var resources = message.Answers.Concat(message.AuthorityRecords).Concat(message.AdditionalRecords).ToArray();
        var service = resources.OfType<SRVRecord>().FirstOrDefault(value => value.Name == instanceName);
        var text = resources.OfType<TXTRecord>().FirstOrDefault(value => value.Name == instanceName);
        if (service is null || text is null || service.Port == 0) return false;

        var properties = ParseProperties(text.Strings);
        if (!TryRequired(properties, "deviceId", out var deviceIdText) || !Guid.TryParse(deviceIdText, out var deviceId) || deviceId == Guid.Empty ||
            !TryRequired(properties, "deviceName", out var deviceName) ||
            !TryRequired(properties, "platform", out var platform) ||
            !TryRequired(properties, "appVersion", out var appVersion) ||
            !TryRequired(properties, "protocolVersion", out var protocolText) ||
            !int.TryParse(protocolText, NumberStyles.None, CultureInfo.InvariantCulture, out var protocolVersion) || protocolVersion <= 0)
            return false;

        var addresses = resources.OfType<AddressRecord>()
            .Where(value => value.Name == service.Target && value.TTL > TimeSpan.Zero)
            .Select(value => value.Address)
            .Distinct()
            .ToArray();
        if (addresses.Length == 0) return false;

        var relevantRecords = resources.Where(value =>
            (value.Name == instanceName && (value is SRVRecord || value is TXTRecord)) ||
            (value.Name == service.Target && value is AddressRecord)).ToArray();
        var ttl = relevantRecords.Where(value => value.TTL > TimeSpan.Zero).Select(value => value.TTL).DefaultIfEmpty(TimeSpan.FromSeconds(75)).Min();

        record = new DnsSdRecord(instanceName.ToString(), deviceId, deviceName, platform,
            protocolVersion, appVersion, service.Port, addresses, observedAtUtc.Add(ttl));
        return true;
    }

    internal static IReadOnlyDictionary<string, string> ParseProperties(IEnumerable<string> strings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in strings)
        {
            var separator = value.IndexOf('=');
            if (separator <= 0 || separator == value.Length - 1) continue;
            result[value[..separator]] = value[(separator + 1)..];
        }
        return result;
    }

    private static bool TryRequired(IReadOnlyDictionary<string, string> properties, string key, out string value)
    {
        if (properties.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value)) return true;
        value = string.Empty;
        return false;
    }
}
