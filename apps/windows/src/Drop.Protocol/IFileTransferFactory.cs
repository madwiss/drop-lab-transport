using Drop.Transport;

namespace Drop.Protocol;

public interface IFileTransferFactory
{
    IFileSender CreateSender(
        DeviceInfo localDevice,
        ITransportConnector connector);

    IFileReceiver CreateReceiver(
        DeviceInfo localDevice);
}