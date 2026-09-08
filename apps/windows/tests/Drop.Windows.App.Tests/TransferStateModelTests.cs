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
