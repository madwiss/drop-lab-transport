using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using Drop.Protocol;

namespace Drop.Windows.App.Tests;

[TestClass]
public sealed class MainWindowBindingTests
{
    [TestMethod]
    public void WindowInitializesReadOnlyBindingsAndUpdatesSenderName()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            MainWindow? window = null;
            try
            {
                // Load the actual compiled XAML without showing the window, which starts networking.
                window = new MainWindow();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Run sender = Descendants(window).OfType<Run>()
                    .Single(run => run.GetBindingExpression(Run.TextProperty) is not null);
                Assert.AreEqual(string.Empty, sender.Text);

                IncomingTransferStateModel incoming = (IncomingTransferStateModel)window.DataContext
                    .GetType().GetProperty("Incoming")!.GetValue(window.DataContext)!;
                var offer = new IncomingTransferOffer(Guid.NewGuid(),
                    new DeviceInfo(Guid.NewGuid(), "Smoke-test sender", "windows", "0.1.0"),
                    Guid.NewGuid(), "photo.jpg", 2048);
                Task<IncomingTransferDecision> decision = incoming.PresentAsync(offer, CancellationToken.None);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.AreEqual("Smoke-test sender", sender.Text);
                incoming.Decline();
                Assert.AreEqual(IncomingTransferDecision.Decline, decision.GetAwaiter().GetResult());
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "WPF binding smoke test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not DependencyObject dependencyObject) continue;
            yield return dependencyObject;
            foreach (DependencyObject descendant in Descendants(dependencyObject)) yield return descendant;
        }
    }
}
