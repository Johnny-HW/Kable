using System.Windows;
using System.Windows.Threading;

namespace Kable.ConfigStudio;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        if (!e.Args.Contains("--smoke-test"))
        {
            base.OnStartup(e);
            new MainWindow().Show();
            return;
        }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);
        try
        {
            var window = new MainWindow { Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
            window.Show();
            await Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            if (!window.IsLoaded || window.DataContext == null) throw new InvalidOperationException("Main window did not initialize.");
            window.Close();
            Shutdown(0);
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("ConfigStudio smoke failed: {0}", error);
            Shutdown(1);
        }
    }
}
