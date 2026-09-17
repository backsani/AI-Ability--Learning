using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NextorialTrainer.Common;
using NextorialTrainer.Models;
using NextorialTrainer.Services;

namespace NextorialTrainer.ViewModels;

public sealed class SetupViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private readonly CliAgentProfile _profile;
    private readonly PracticeHistoryProfile _history;
    private readonly List<PracticeReport> _reports;
    private int _planSeed = Environment.TickCount;

    private string _newDomain = "";
    private string _difficulty = "일반 실무 수준";
    private string _collabModel = "";
    private string _evalModel = "";
    private bool _useTargetTime;
    private int _targetMinutes = 60;
    private string _error = "";

    public SetupViewModel(MainWindowViewModel shell, string? presetDomain = null, string? presetTypeKey = null)
    {
        _shell = shell;
        _profile = shell.Settings.ResolveProfile();

        _reports = shell.Reports.LoadAll();
        _history = PracticeHistoryProfile.Build(_reports);

        var s = shell.Settings.Current;
        var agent = shell.Settings.ActiveAgent;

        foreach (var name in DomainCatalog.Presets)
            Domains.Add(new DomainOption(name));

        foreach (var name in s.CustomDomains)
            if (Domains.All(d => d.Name != name))
                Domains.Add(new DomainOption(name, isCustom: true));

        var wantedDomains = presetDomain is not null ? new List<string> { presetDomain } : s.LastSelectedDomains;
        if (wantedDomains.Count == 0 && Domains.Count > 0) wantedDomains = new List<string> { Domains[0].Name };

        foreach (var name in wantedDomains)
        {
            var existing = Domains.FirstOrDefault(d => d.Name == name);
            if (existing is null)
            {
                existing = new DomainOption(name, isCustom: true);
                Domains.Add(existing);
            }
            existing.IsSelected = true;
        }

        foreach (var t in ProblemTypeCatalog.All)
            Types.Add(new TypeOption(t.Key, t.Name, t.ShortDesc));

        var wantedTypes = presetTypeKey is not null
            ? new List<string> { presetTypeKey }
            : (s.LastSelectedTypes.Count > 0 ? s.LastSelectedTypes : ProblemTypeCatalog.All.Select(t => t.Key).ToList());

        foreach (var key in wantedTypes)
        {
            var t = Types.FirstOrDefault(x => x.Key == key);
            if (t is not null) t.IsSelected = true;
        }

        _difficulty = s.LastDifficulty;
        _useTargetTime = s.LastUseTargetTime;
        _targetMinutes = s.LastTargetMinutes;

        Models = new ObservableCollection<string>(_profile.Models);
        _collabModel = Pick(agent.CollabModel);
        _evalModel = Pick(agent.EvalModel);

        AddDomainCommand = new RelayCommand(AddDomain);
        RemoveDomainCommand = new RelayCommand(RemoveDomain);
        StartCommand = new RelayCommand(Start);
        BackCommand = new RelayCommand(_shell.GoHome);
        SettingsCommand = new RelayCommand(_shell.GoToSettings);
        RerollCommand = new RelayCommand(Reroll);
        ProgressCommand = new RelayCommand(_shell.GoToProgress);

        foreach (var option in Domains) option.PropertyChanged += (_, _) => RebuildPlan();
        foreach (var option in Types) option.PropertyChanged += (_, _) => RebuildPlan();

        RebuildPlan();
    }

    public ObservableCollection<DomainOption> Domains { get; } = new();
    public ObservableCollection<TypeOption> Types { get; } = new();

    public PlannedProblem? Planned { get; private set; }

    public bool HasPlan => Planned is not null;

    public string PlanHint => _history.ReportCount == 0
        ? "지난 연습 기록이 없어서 무작위로 골랐습니다. 연습할수록 안 풀어본 조합이 먼저 나옵니다."
        : $"지난 연습 {_history.ReportCount}건을 반영해 골랐습니다. 안 풀어본 조합 > 최근 부진했던 조합 > 오래된 조합 순입니다.";

    private void Reroll()
    {
        _planSeed = Environment.TickCount;
        RebuildPlan();
    }

    private void RebuildPlan()
    {
        var domains = Domains.Where(d => d.IsSelected).Select(d => d.Name).ToList();
        var types = Types.Where(t => t.IsSelected).Select(t => t.Key).ToList();

        Planned = domains.Count > 0 && types.Count > 0
            ? ProblemPlanner.Pick(domains, types, _history, _planSeed)
            : null;

        OnPropertyChanged(nameof(Planned));
        OnPropertyChanged(nameof(HasPlan));
    }

    public ObservableCollection<string> Models { get; }
    public bool HasModels => Models.Count > 0;
    public string AgentLabel => _profile.DisplayName;

    public string CollabWarning => _profile.CollabVerified
        ? ""
        : "이 CLI는 실제 파일 조작이 검증되지 않았습니다. 채팅만 되고 코드가 실제로 반영되지 않을 수 있습니다.";

    public bool HasCollabWarning => CollabWarning.Length > 0;

    public IReadOnlyList<string> Difficulties { get; } = new[]
    {
        "주니어 실무 수준",
        "일반 실무 수준",
        "시니어 실무 수준"
    };

    public string NewDomain
    {
        get => _newDomain;
        set => SetProperty(ref _newDomain, value);
    }

    public string Difficulty
    {
        get => _difficulty;
        set => SetProperty(ref _difficulty, value);
    }

    public string CollabModel
    {
        get => _collabModel;
        set => SetProperty(ref _collabModel, value);
    }

    public string EvalModel
    {
        get => _evalModel;
        set => SetProperty(ref _evalModel, value);
    }

    public bool UseTargetTime
    {
        get => _useTargetTime;
        set => SetProperty(ref _useTargetTime, value);
    }

    public int TargetMinutes
    {
        get => _targetMinutes;
        set => SetProperty(ref _targetMinutes, value);
    }

    public string Error
    {
        get => _error;
        set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public RelayCommand AddDomainCommand { get; }
    public RelayCommand RemoveDomainCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand RerollCommand { get; }
    public RelayCommand ProgressCommand { get; }

    private string Pick(string fromSettings)
    {
        if (!string.IsNullOrWhiteSpace(fromSettings) && Models.Contains(fromSettings)) return fromSettings;
        return Models.Count > 0 ? Models[0] : "";
    }

    private void AddDomain()
    {
        var name = NewDomain.Trim();
        if (name.Length == 0) return;

        var existing = Domains.FirstOrDefault(d => d.Name == name);
        if (existing is null)
        {
            existing = new DomainOption(name, isCustom: true);
            existing.PropertyChanged += (_, _) => RebuildPlan();
            Domains.Add(existing);

            var custom = _shell.Settings.Current.CustomDomains;
            if (!custom.Contains(name))
            {
                custom.Add(name);
                _shell.Settings.Save();
            }
        }

        existing.IsSelected = true;
        NewDomain = "";
        Error = "";
    }

    private void RemoveDomain()
    {
        var doomed = Domains.Where(d => d.IsCustom && d.IsSelected).ToList();
        if (doomed.Count == 0)
        {
            Error = "직접 추가한 분야 중 선택된 것만 삭제할 수 있습니다.";
            return;
        }

        foreach (var d in doomed)
        {
            Domains.Remove(d);
            _shell.Settings.Current.CustomDomains.Remove(d.Name);
        }
        _shell.Settings.Save();
        Error = "";
    }

    private void Start()
    {
        var domains = Domains.Where(d => d.IsSelected).Select(d => d.Name).ToList();
        var types = Types.Where(t => t.IsSelected).Select(t => t.Key).ToList();

        if (domains.Count == 0) { Error = "분야를 하나 이상 선택해 주세요."; return; }
        if (types.Count == 0) { Error = "문제 유형을 하나 이상 선택해 주세요."; return; }

        try { _shell.Backend.ResolvePath(); }
        catch (AgentCliException ex) { Error = ex.Message; return; }

        RebuildPlan();
        if (Planned is null) { Error = "문제 조합을 정하지 못했습니다."; return; }

        var s = _shell.Settings.Current;
        s.LastSelectedDomains = domains;
        s.LastSelectedTypes = types;
        s.LastDifficulty = Difficulty;
        s.LastUseTargetTime = UseTargetTime;
        s.LastTargetMinutes = TargetMinutes;

        var agent = _shell.Settings.ActiveAgent;
        agent.CollabModel = CollabModel;
        agent.EvalModel = EvalModel;
        _shell.Settings.Save();

        var avoidTitles = _history.TitlesFor(_reports, Planned.Domain, Planned.TypeKey);

        _shell.GoToPractice(new PracticeConfig
        {
            Domains = domains,
            TypeKeys = types,
            Difficulty = Difficulty,
            UseTargetTime = UseTargetTime,
            TargetMinutes = TargetMinutes,
            AgentId = s.SelectedAgentId,
            CollabModel = CollabModel,
            EvalModel = EvalModel,
            PlannedDomain = Planned.Domain,
            PlannedTypeKey = Planned.TypeKey,
            AvoidTitles = avoidTitles
        });
    }
}
