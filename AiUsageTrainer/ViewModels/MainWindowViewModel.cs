using Avalonia;
using Avalonia.Styling;
using AiUsageTrainer.Common;
using AiUsageTrainer.Models;
using AiUsageTrainer.Services;

namespace AiUsageTrainer.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase? _currentPage;

    public MainWindowViewModel()
    {
        Settings = new SettingsService();
        Settings.Load();

        Backend = new CliAgentBackend(
            () => Settings.ResolveProfile(),
            () => Settings.ActiveAgent.ExecutablePath);

        Reports = new ReportStore();

        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        OpenSettingsCommand = new RelayCommand(GoToSettings);
        OpenHistoryCommand = new RelayCommand(GoToHistory);
        OpenProgressCommand = new RelayCommand(GoToProgress);
        GoHomeCommand = new RelayCommand(GoHome);

        ApplyTheme(Settings.Current.Theme);
        GoHome();
    }

    public SettingsService Settings { get; }
    public CliAgentBackend Backend { get; }
    public ReportStore Reports { get; }

    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenHistoryCommand { get; }
    public RelayCommand OpenProgressCommand { get; }
    public RelayCommand GoHomeCommand { get; }

    public ViewModelBase? CurrentPage
    {
        get => _currentPage;
        private set
        {
            var old = _currentPage;
            if (!SetProperty(ref _currentPage, value)) return;
            (old as System.IDisposable)?.Dispose();
        }
    }

    public string ThemeGlyph => IsDark ? "☀" : "☽";
    public bool IsDark => Settings.Current.Theme != "Light";

    public void GoHome() => CurrentPage = new HomeViewModel(this);

    public void GoToSetup(string? presetDomain = null, string? presetTypeKey = null)
        => CurrentPage = new SetupViewModel(this, presetDomain, presetTypeKey);

    public void GoToPractice(PracticeConfig config)
        => CurrentPage = new PracticeViewModel(this, config);

    public void GoToResult(PracticeReport report, bool justFinished)
        => CurrentPage = new ResultViewModel(this, report, justFinished);

    public void GoToHistory() => CurrentPage = new HistoryViewModel(this);

    public void GoToProgress() => CurrentPage = new ProgressViewModel(this);

    public void GoToSettings() => CurrentPage = new SettingsViewModel(this);

    public void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app is null) return;

        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        OnPropertyChanged(nameof(IsDark));
        OnPropertyChanged(nameof(ThemeGlyph));
    }

    private void ToggleTheme()
    {
        Settings.Current.Theme = IsDark ? "Light" : "Dark";
        Settings.Save();
        ApplyTheme(Settings.Current.Theme);
    }
}
