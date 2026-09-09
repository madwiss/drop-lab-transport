using Drop.Protocol;

namespace Drop.Windows.App.Tests;

[TestClass]
public sealed class TransferStateModelTests
{
    [TestMethod]
    public void NormalTransferTracksStagesAndProgress()
    {
        TransferStateModel model = new();

        model.Begin("photo.jpg", 2048);
        Assert.AreEqual(TransferState.Connecting, model.State);
        Assert.IsTrue(model.IsBusy);

        model.ReportStage(SendStage.WaitingForAcceptance);
        Assert.AreEqual(TransferState.WaitingForAcceptance, model.State);

        model.ReportStage(SendStage.PreparingFile);
        model.ReportStage(SendStage.Transferring);
        model.ReportProgress(new FileTransferProgress(Guid.NewGuid(), 1024, 2048));
        Assert.AreEqual(50d, model.ProgressPercent);
        Assert.AreEqual("1 KB / 2 KB", model.ProgressText);

        model.ReportStage(SendStage.Completing);
        model.Complete();
        Assert.AreEqual(TransferState.Completed, model.State);
        Assert.AreEqual(100d, model.ProgressPercent);
        Assert.IsFalse(model.IsBusy);
    }

    [TestMethod]
    public void ConnectingStatusShowsTheSelectedRemoteEndpoint()
    {
        TransferStateModel model = new();

        model.Begin("photo.jpg", 2048, "192.168.0.139:61138");
        model.ReportStage(SendStage.Connecting);

        StringAssert.Contains(model.StatusText, "192.168.0.139:61138");
    }

    [TestMethod]
    public void ActiveTransferPreventsAConflictingSend()
    {
        TransferStateModel model = new();
        model.Begin("one.bin", 1);

        Assert.ThrowsExactly<InvalidOperationException>(() => model.Begin("two.bin", 1));
    }

    [TestMethod]
    public void CancellationAndFailureBecomeTerminalStates()
    {
        TransferStateModel model = new();
        model.Begin("one.bin", 1);
        model.Cancel();
        Assert.AreEqual(TransferState.Cancelled, model.State);
        Assert.IsFalse(model.CanCancel);

        model.Begin("two.bin", 1);
        model.Fail("Device unavailable");
        Assert.AreEqual(TransferState.Failed, model.State);
        StringAssert.Contains(model.StatusText, "Device unavailable");
        Assert.IsFalse(model.IsBusy);
    }
}

[TestClass]
public sealed class IncomingTransferStateModelTests
{
    private static readonly DeviceInfo Sender = new(Guid.NewGuid(), "Sender PC", "windows", "0.1.0");

    [TestMethod]
    public async Task AcceptMovesOfferToReceivingAndTracksProgress()
    {
        IncomingTransferStateModel model = new();
        IncomingTransferOffer offer = new(Guid.NewGuid(), Sender, Guid.NewGuid(), "photo.jpg", 2048);

        Task<IncomingTransferDecision> decision = model.PresentAsync(offer, CancellationToken.None);
        Assert.AreEqual(IncomingTransferState.IncomingOffer, model.State);
        Assert.AreEqual("Sender PC", model.SenderName);
        Assert.AreEqual("photo.jpg", model.FileName);
        Assert.IsTrue(model.CanDecide);

        model.Accept();
        Assert.AreEqual(IncomingTransferDecision.Accept, await decision);
        Assert.AreEqual(IncomingTransferState.Receiving, model.State);

        model.ReportProgress(new FileTransferProgress(offer.FileId, 1024, 2048));
        Assert.AreEqual(50d, model.ProgressPercent);
        model.Complete();
        Assert.AreEqual(IncomingTransferState.Completed, model.State);
    }

    [TestMethod]
    public async Task DeclineMovesOfferToDeclined()
    {
        IncomingTransferStateModel model = new();
        IncomingTransferOffer offer = new(Guid.NewGuid(), Sender, Guid.NewGuid(), "nope.bin", 42);

        Task<IncomingTransferDecision> decision = model.PresentAsync(offer, CancellationToken.None);
        model.Decline();

        Assert.AreEqual(IncomingTransferDecision.Decline, await decision);
        Assert.AreEqual(IncomingTransferState.Declined, model.State);
        Assert.IsFalse(model.CanDecide);
    }
}
