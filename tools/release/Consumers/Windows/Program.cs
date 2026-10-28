using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kable.UI.Wpf.Controls;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/Kable.UI.Wpf;component/Themes/IosTheme.xaml", UriKind.Relative) });
            var panel = new StackPanel();
            foreach (var type in typeof(LiveDiagnosticsBar).Assembly.GetExportedTypes()
                .Where(type => !type.IsAbstract && typeof(UserControl).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null))
                panel.Children.Add((UserControl)Activator.CreateInstance(type)!);
            if (panel.Children.Count == 0) throw new Exception("No WPF controls found.");
            var window = new Window { Content = panel, Width = 800, Height = 600, Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
            window.Show();
            window.UpdateLayout();
            application.Dispatcher.Invoke(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            if (!window.IsLoaded) throw new Exception("WPF controls did not load.");
            window.Close();
            application.Shutdown();
            Console.WriteLine($"WPF package consumer OK: {panel.Children.Count} controls; {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
