using System.Net;
using Makaretu.Dns;

namespace Drop.Discovery.Tests;

[TestClass]
public sealed class MdnsRecordParserTests
{
    [TestMethod]
    public void CreateProfile_AdvertisesRequiredDropFieldsAndPort()
    {
        var deviceId = Guid.NewGuid();
        var profile = MakaretuDnsSdBackend.CreateProfile(
            new(deviceId, "NICO-PC", "windows", 1, "0.1.0", 5050));
        var service = profile.Resources.OfType<SRVRecord>().Single();
        var properties = MdnsRecordParser.ParseProperties(profile.Resources.OfType<TXTRecord>().Single().Strings);

        Assert.AreEqual("_drop._tcp", profile.ServiceName.ToString());
        Assert.AreEqual(5050, service.Port);
        Assert.AreEqual(deviceId.ToString("D"), properties["deviceId"]);
        Assert.AreEqual("NICO-PC", properties["deviceName"]);
        Assert.AreEqual("windows", properties["platform"]);
        Assert.AreEqual("1", properties["protocolVersion"]);
        Assert.AreEqual("0.1.0", properties["appVersion"]);
    }

    [TestMethod]
    public void TryParse_ReadsDropMetadataPortAddressAndTtl()
    {
        var deviceId = Guid.NewGuid();
        var observed = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var profile = Profile(deviceId, "NICO-PC", IPAddress.Parse("192.168.1.8"));
        foreach (var resource in profile.Resources) resource.TTL = TimeSpan.FromSeconds(120);
        var message = new Message();
        message.AdditionalRecords.AddRange(profile.Resources);

        var parsed = MdnsRecordParser.TryParse(profile.FullyQualifiedName, message, observed, out var record);

        Assert.IsTrue(parsed);
        Assert.AreEqual(deviceId, record.DeviceId);
        Assert.AreEqual("NICO-PC", record.DeviceName);
        Assert.AreEqual("windows", record.Platform);
        Assert.AreEqual(1, record.ProtocolVersion);
        Assert.AreEqual("0.1.0", record.AppVersion);
        Assert.AreEqual(5050, record.Port);
        Assert.AreEqual(IPAddress.Parse("192.168.1.8"), record.Addresses.Single());
        Assert.AreEqual(observed.AddSeconds(120), record.ExpiresAtUtc);
    }

    [TestMethod]
    public void TryParse_RejectsMissingRequiredMetadata()
    {
        var profile = new ServiceProfile("peer", MakaretuDnsSdBackend.ServiceType, 5050,
            [IPAddress.Parse("192.168.1.8")]);
        profile.AddProperty("deviceName", "Peer");
        var message = new Message();
        message.AdditionalRecords.AddRange(profile.Resources);

        Assert.IsFalse(MdnsRecordParser.TryParse(
            profile.FullyQualifiedName, message, DateTimeOffset.UtcNow, out _));
    }

    [TestMethod]
    public void ParseProperties_IgnoresFlagsAndMalformedValuesAndUsesLatestDuplicate()
    {
        var properties = MdnsRecordParser.ParseProperties(["txtvers=1", "flag", "=bad", "name=first", "name=second"]);

        Assert.AreEqual("1", properties["txtvers"]);
        Assert.AreEqual("second", properties["name"]);
        Assert.IsFalse(properties.ContainsKey("flag"));
    }

    private static ServiceProfile Profile(Guid deviceId, string name, IPAddress address)
    {
        var profile = new ServiceProfile(deviceId.ToString("D"), MakaretuDnsSdBackend.ServiceType, 5050, [address]);
        profile.AddProperty("deviceId", deviceId.ToString("D"));
        profile.AddProperty("deviceName", name);
        profile.AddProperty("platform", "windows");
        profile.AddProperty("protocolVersion", "1");
        profile.AddProperty("appVersion", "0.1.0");
        return profile;
    }
}
