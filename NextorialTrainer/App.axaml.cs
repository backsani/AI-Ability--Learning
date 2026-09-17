using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NextorialTrainer.ViewModels;
using NextorialTrainer.Views;

namespace NextorialTrainer;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = shell };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
