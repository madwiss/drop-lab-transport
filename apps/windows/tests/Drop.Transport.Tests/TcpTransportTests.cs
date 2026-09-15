using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Drop.Transport.Tests;

[TestClass]
public sealed class TcpTransportTests
{
    [TestMethod]
    public async Task ConnectorAndListenerExchangeBytesOverLoopbackAsync()
    {
        await using TcpTransportListener listener = new(IPAddress.Loopback, 0);
        listener.Start();

        ValueTask<IReliableByteStream> acceptTask = listener.AcceptAsync();
        await using IReliableByteStream outbound = await new TcpTransportConnector()
            .ConnectAsync(listener.LocalEndpoint);
        await using IReliableByteStream inbound = await acceptTask;

        byte[] sent = Encoding.UTF8.GetBytes("drop-transport");
        await outbound.Stream.WriteAsync(sent);

        byte[] received = new byte[sent.Length];
        await ReadExactlyAsync(inbound.Stream, received);

        CollectionAssert.AreEqual(sent, received);
    }

    [TestMethod]
    public async Task ListenerAcceptHonorsCancellationAsync()
    {
        await using TcpTransportListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await listener.AcceptAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task ConnectorMapsSocketFailureToTypedTransportFailureAsync()
    {
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        TransportFailureException failure = await Assert.ThrowsAsync<TransportFailureException>(async () =>
            await new TcpTransportConnector().ConnectAsync(new TcpTransportEndpoint(IPAddress.Loopback, port)));

        Assert.AreEqual(TransportFailureKind.Connection, failure.Kind);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer[read..]);
            if (count == 0) throw new EndOfStreamException();
            read += count;
        }
    }
}
