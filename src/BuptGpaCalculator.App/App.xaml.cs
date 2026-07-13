using System.Windows;

namespace BuptGpaCalculator.App;

/// <summary>Provides the application entry point.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
