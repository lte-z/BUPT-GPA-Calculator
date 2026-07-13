using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace BuptGpaCalculator.App;

/// <summary>Provides the application entry point.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        var window = new MainWindow();
        SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, true);
        window.Show();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.ReportUnexpectedException(e.Exception);
            return;
        }

        System.Windows.MessageBox.Show(
            e.Exception.Message,
            "BUPT GPA Calculator 启动失败",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
        Shutdown(-1);
    }
}
