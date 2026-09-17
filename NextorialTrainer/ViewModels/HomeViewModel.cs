using NextorialTrainer.Common;
using NextorialTrainer.Services;

namespace NextorialTrainer.ViewModels;

public sealed class HomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;

    public HomeViewModel(MainWindowViewModel shell)
    {
        _shell = shell;

        StartCommand = new RelayCommand(() => _shell.GoToSetup());
        HistoryCommand = new RelayCommand(_shell.GoToHistory);
        SettingsCommand = new RelayCommand(_shell.GoToSettings);
        ProgressCommand = new RelayCommand(_shell.GoToProgress);

        var reports = _shell.Reports.LoadAll();
        ReportCount = reports.Count;
        LastReportLine = reports.Count == 0
            ? "아직 완료한 연습이 없습니다."
            : $"최근: {reports[0].DateLabel} · {reports[0].Title} · {reports[0].GoodCriteriaLabel}";

        CliStatus = BuildCliStatus();
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand HistoryCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand ProgressCommand { get; }

    public int ReportCount { get; }
    public string LastReportLine { get; }
    public string CliStatus { get; }
    public bool CliReady { get; private set; }

    private string BuildCliStatus()
    {
        var profile = _shell.Settings.ResolveProfile();
        try
        {
            var path = _shell.Backend.ResolvePath();
            CliReady = true;
            return $"{profile.DisplayName} 연결됨 · {path}" +
                   (profile.CollabVerified ? "" : " · 이 CLI는 협업 대화의 실제 파일 조작이 검증되지 않았습니다");
        }
        catch (AgentCliException ex)
        {
            CliReady = false;
            return ex.Message;
        }
    }
}
