using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NextorialTrainer.Common;
using NextorialTrainer.Models;
using NextorialTrainer.Services;

namespace NextorialTrainer.ViewModels;

/// <summary>One domain's coverage across the four problem types, for the progress screen.</summary>
public sealed class DomainProgress
{
    public required string Domain { get; init; }
    public required List<DomainTypeStatus> Types { get; init; }

    public int Practiced => Types.Count(t => t.TimesPracticed > 0);
    public int Total => Types.Count;

    public string HeadLine => $"{Practiced} / {Total} 유형 풀어봄";
}

public sealed class ProgressViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private readonly PracticeHistoryProfile _profile;
    private bool _onlySelectedDomains = true;

    public ProgressViewModel(MainWindowViewModel shell)
    {
        _shell = shell;

        var reports = shell.Reports.LoadAll();
        _profile = PracticeHistoryProfile.Build(reports);
        ReportCount = reports.Count;

        HomeCommand = new RelayCommand(_shell.GoHome);
        StartCommand = new RelayCommand(() => _shell.GoToSetup());
        ToggleScopeCommand = new RelayCommand(() => OnlySelectedDomains = !OnlySelectedDomains);

        Rebuild();
    }

    public ObservableCollection<DomainProgress> Domains { get; } = new();

    public int ReportCount { get; }

    public RelayCommand HomeCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand ToggleScopeCommand { get; }

    public bool OnlySelectedDomains
    {
        get => _onlySelectedDomains;
        private set
        {
            if (!SetProperty(ref _onlySelectedDomains, value)) return;
            OnPropertyChanged(nameof(ScopeButtonText));
            Rebuild();
        }
    }

    public string ScopeButtonText => OnlySelectedDomains ? "전체 분야 보기" : "최근 선택한 분야만 보기";

    public bool IsEmpty => Domains.Count == 0;

    public string SummaryLine => ReportCount == 0
        ? "아직 연습 기록이 없습니다. 한 번 풀고 나면 여기에 진도가 쌓입니다."
        : $"연습 {ReportCount}건 기준 · {Domains.Sum(d => d.Practiced)} / {Domains.Sum(d => d.Total)} 조합 확인";

    private void Rebuild()
    {
        var last = _shell.Settings.Current.LastSelectedDomains;

        var wanted = OnlySelectedDomains && last.Count > 0
            ? last
            : DomainCatalog.Presets.ToList();

        Domains.Clear();
        foreach (var domain in wanted)
        {
            Domains.Add(new DomainProgress
            {
                Domain = domain,
                Types = ProblemTypeCatalog.All
                    .Select(t => _profile.StatusOf(domain, t.Key))
                    .OrderBy(s => s.TimesPracticed)
                    .ToList()
            });
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryLine));
    }
}
