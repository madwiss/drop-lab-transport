using Drop.Protocol;
using Drop.Transport;

namespace Drop.Windows;

public sealed class TransportFileTransferFactory : IFileTransferFactory
{
    public IFileSender CreateSender(
        DeviceInfo localDevice,
        ITransportConnector connector)
    {
        ArgumentNullException.ThrowIfNull(localDevice);
        ArgumentNullException.ThrowIfNull(connector);

        return new TcpFileSender(localDevice, connector);
    }

    public IFileReceiver CreateReceiver(
        DeviceInfo localDevice)
    {
        ArgumentNullException.ThrowIfNull(localDevice);

        return new TcpFileReceiver(localDevice);
    }
}
