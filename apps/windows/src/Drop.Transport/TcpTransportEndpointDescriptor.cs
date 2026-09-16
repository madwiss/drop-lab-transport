using System.Net;

namespace Drop.Transport;

/// <summary>
/// TCP endpoint information.
/// </summary>
public sealed record TcpTransportEndpointDescriptor(
    IPAddress Address,
    int Port) : TransportEndpointDescriptor;