using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiUsageTrainer.Common;
using AiUsageTrainer.Models;
using AiUsageTrainer.Services;

namespace AiUsageTrainer.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private bool _loading = true;

    private CliAgentProfile _selectedProfile;
    private string _cliPath = "";
    private string _collabModel = "";
    private string _evalModel = "";
    private string _theme;

    private string _modelsCsv = "";
    private string _firstArgs = "";
    private string _resumeArgs = "";
    private string _collabFirstArgs = "";
    private string _collabResumeArgs = "";
    private string _sessionMode = "";
    private string _promptVia = "";
    private string _replyFormat = "";
    private string _replyJsonField = "";

    private string _notice = "";
    private string _testOutput = "";
    private bool _testing;

    public SettingsViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        _theme = shell.Settings.Current.Theme;

        Profiles = new ObservableCollection<CliAgentProfile>(CliAgentCatalog.Profiles);
        _selectedProfile = CliAgentCatalog.Find(shell.Settings.Current.SelectedAgentId);

        DetectCommand = new RelayCommand(Detect);
        TestCommand = new AsyncRelayCommand(TestAsync, () => !_testing);
        ResetAdvancedCommand = new RelayCommand(ResetAdvanced);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        BackCommand = new RelayCommand(SaveAndLeave);

        LoadAgentFields();
        _loading = false;
    }

    public ObservableCollection<CliAgentProfile> Profiles { get; }
    public ObservableCollection<string> ModelChoices { get; } = new();

    public IReadOnlyList<string> Themes { get; } = new[] { "Dark", "Light", "System" };
    public IReadOnlyList<string> SessionModes { get; } = new[] { "Resume", "Replay" };
    public IReadOnlyList<string> PromptVias { get; } = new[] { "Stdin", "Arg" };
    public IReadOnlyList<string> ReplyFormats { get; } = new[] { "Json", "Raw" };

    public string DataFolder => AppPaths.Root;

    public CliAgentProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (value is null || !SetProperty(ref _selectedProfile, value)) return;

            _shell.Settings.Current.SelectedAgentId = value.Id;
            _shell.Settings.Save();

            LoadAgentFields();
            Notice = $"{value.DisplayName}(으)로 전환했습니다.";
            TestOutput = "";
        }
    }

    public string AgentDescription => SelectedProfile.Description;
    public bool IsVerified => SelectedProfile.Verified;
    public bool IsCollabVerified => SelectedProfile.CollabVerified;
    public string VerifiedLabel => IsVerified ? "검증됨" : "미검증 프리셋";
    public string CollabVerifiedLabel => IsCollabVerified
        ? "협업 시 실제 파일 조작 검증됨"
        : "협업 시 실제 파일 조작은 검증되지 않음";

    public bool ShowModels => ModelChoices.Count > 0;

    public string ModelHint => SelectedProfile.Id == CliAgentCatalog.ClaudeId
        ? "협업은 sonnet으로 충분합니다. 평가만 opus로 올리면 더 꼼꼼해지지만 비용이 늘어납니다."
        : "이 CLI가 받는 모델 이름입니다. 비어 있으면 CLI의 기본 모델을 씁니다.";

    public string CliPath
    {
        get => _cliPath;
        set => SetProperty(ref _cliPath, value);
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

    public string Theme
    {
        get => _theme;
        set
        {
            if (!SetProperty(ref _theme, value) || _loading) return;
            _shell.Settings.Current.Theme = value;
            _shell.Settings.Save();
            _shell.ApplyTheme(value);
        }
    }

    // --- 고급 ---

    public string ModelsCsv
    {
        get => _modelsCsv;
        set
        {
            if (!SetProperty(ref _modelsCsv, value) || _loading) return;
            RefreshModelChoices();
        }
    }

    public string FirstArgs
    {
        get => _firstArgs;
        set => SetProperty(ref _firstArgs, value);
    }

    public string ResumeArgs
    {
        get => _resumeArgs;
        set => SetProperty(ref _resumeArgs, value);
    }

    public string CollabFirstArgs
    {
        get => _collabFirstArgs;
        set => SetProperty(ref _collabFirstArgs, value);
    }

    public string CollabResumeArgs
    {
        get => _collabResumeArgs;
        set => SetProperty(ref _collabResumeArgs, value);
    }

    public string SessionMode
    {
        get => _sessionMode;
        set { if (SetProperty(ref _sessionMode, value)) OnPropertyChanged(nameof(SessionModeHint)); }
    }

    public string SessionModeHint => SessionMode == "Resume"
        ? "CLI가 대화를 보관합니다. 매 턴 새 메시지만 보내므로 저렴합니다. --resume 류의 옵션이 있는 CLI에서만 됩니다."
        : "앱이 대화를 보관하고 매 턴 전체 기록을 다시 보냅니다. 어떤 CLI에서도 되지만 턴이 길어질수록 비용이 늘어납니다.";

    public string PromptVia
    {
        get => _promptVia;
        set => SetProperty(ref _promptVia, value);
    }

    public string ReplyFormat
    {
        get => _replyFormat;
        set { if (SetProperty(ref _replyFormat, value)) OnPropertyChanged(nameof(IsJsonReply)); }
    }

    public bool IsJsonReply => ReplyFormat == "Json";

    public string ReplyJsonField
    {
        get => _replyJsonField;
        set => SetProperty(ref _replyJsonField, value);
    }

    public string Notice
    {
        get => _notice;
        private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); }
    }

    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    public string TestOutput
    {
        get => _testOutput;
        private set { if (SetProperty(ref _testOutput, value)) OnPropertyChanged(nameof(HasTestOutput)); }
    }

    public bool HasTestOutput => !string.IsNullOrEmpty(TestOutput);

    public RelayCommand DetectCommand { get; }
    public AsyncRelayCommand TestCommand { get; }
    public RelayCommand ResetAdvancedCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand BackCommand { get; }

    private void LoadAgentFields()
    {
        _loading = true;

        var preset = SelectedProfile;
        var s = _shell.Settings.Current.AgentFor(preset.Id);

        CliPath = s.ExecutablePath;
        ModelsCsv = s.ModelsCsv;
        FirstArgs = string.IsNullOrWhiteSpace(s.FirstArgs) ? preset.FirstArgs : s.FirstArgs;
        ResumeArgs = string.IsNullOrWhiteSpace(s.ResumeArgs) ? preset.ResumeArgs : s.ResumeArgs;
        CollabFirstArgs = string.IsNullOrWhiteSpace(s.CollabFirstArgs) ? preset.CollabFirstArgs : s.CollabFirstArgs;
        CollabResumeArgs = string.IsNullOrWhiteSpace(s.CollabResumeArgs) ? preset.CollabResumeArgs : s.CollabResumeArgs;

        var effective = preset.WithOverrides(s);
        SessionMode = effective.SessionMode.ToString();
        PromptVia = effective.PromptVia.ToString();
        ReplyFormat = effective.ReplyFormat.ToString();
        ReplyJsonField = effective.ReplyJsonField;

        RefreshModelChoices();
        CollabModel = Coerce(s.CollabModel, preset.DefaultCollabModel);
        EvalModel = Coerce(s.EvalModel, preset.DefaultEvalModel);

        _loading = false;

        OnPropertyChanged(nameof(AgentDescription));
        OnPropertyChanged(nameof(IsVerified));
        OnPropertyChanged(nameof(IsCollabVerified));
        OnPropertyChanged(nameof(VerifiedLabel));
        OnPropertyChanged(nameof(CollabVerifiedLabel));
        OnPropertyChanged(nameof(ModelHint));
        OnPropertyChanged(nameof(SessionModeHint));
        OnPropertyChanged(nameof(IsJsonReply));
    }

    private void RefreshModelChoices()
    {
        var custom = CliAgentProfile.SplitCsv(ModelsCsv);
        var list = custom.Count > 0 ? custom : SelectedProfile.Models;

        ModelChoices.Clear();
        foreach (var m in list) ModelChoices.Add(m);

        OnPropertyChanged(nameof(ShowModels));

        if (!ModelChoices.Contains(CollabModel)) CollabModel = ModelChoices.FirstOrDefault() ?? "";
        if (!ModelChoices.Contains(EvalModel)) EvalModel = ModelChoices.FirstOrDefault() ?? "";
    }

    private string Coerce(string wanted, string fallback)
    {
        if (ModelChoices.Count == 0) return "";
        if (ModelChoices.Contains(wanted)) return wanted;
        if (ModelChoices.Contains(fallback)) return fallback;
        return ModelChoices[0];
    }

    private void Detect()
    {
        var found = CliAgentBackend.DetectPath(SelectedProfile);
        if (found is null)
        {
            var names = string.Join(", ", SelectedProfile.ExecutableNames.DefaultIfEmpty("(이름 미지정)"));
            Notice = $"실행 파일을 찾지 못했습니다. 찾아본 이름: {names}. 경로를 직접 입력해 주세요.";
            return;
        }
        CliPath = found;
        Notice = "자동으로 찾았습니다: " + found;
    }

    /// <summary>Runs one real eval-mode turn so a wrong preset shows up here instead of mid-session.</summary>
    private async Task TestAsync()
    {
        _testing = true;
        TestCommand.RaiseCanExecuteChanged();
        Notice = "테스트 중...";
        TestOutput = "";

        try
        {
            ApplyToSettings();

            var backend = new CliAgentBackend(
                () => _shell.Settings.ResolveProfile(),
                () => _shell.Settings.ActiveAgent.ExecutablePath);

            var conversation = backend.CreateEvalConversation(
                "너는 테스트 응답기다. 짧게 한 문장으로만 답한다.");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var reply = await conversation.SendAsync("연결 테스트: 잘 받았으면 '연결됨'이라고만 답해줘.", EvalModel, cts.Token)
                .ConfigureAwait(true);

            Notice = "연결 성공. 문제 출제·평가 진행에 문제 없습니다.";
            TestOutput = "실행한 명령:\n" + backend.LastCommandLine + "\n\n응답:\n" + Shorten(reply);
        }
        catch (AgentCliException ex)
        {
            Notice = "테스트 실패: " + ex.Message;
            TestOutput =
                (ex.CommandLine is null ? "" : "실행한 명령:\n" + ex.CommandLine + "\n\n") +
                (string.IsNullOrWhiteSpace(ex.Detail) ? "" : "출력:\n" + Shorten(ex.Detail));
        }
        catch (OperationCanceledException)
        {
            Notice = "테스트가 3분을 넘겨 중단했습니다.";
        }
        catch (Exception ex)
        {
            Notice = "테스트 실패: " + ex.Message;
        }
        finally
        {
            _testing = false;
            TestCommand.RaiseCanExecuteChanged();
        }
    }

    private void ResetAdvanced()
    {
        var s = _shell.Settings.Current.AgentFor(SelectedProfile.Id);
        s.ModelsCsv = "";
        s.FirstArgs = "";
        s.ResumeArgs = "";
        s.CollabFirstArgs = "";
        s.CollabResumeArgs = "";
        s.SessionMode = "";
        s.PromptVia = "";
        s.ReplyFormat = "";
        s.ReplyJsonField = "";
        _shell.Settings.Save();

        LoadAgentFields();
        Notice = "고급 설정을 기본값으로 되돌렸습니다.";
    }

    private void ApplyToSettings()
    {
        var preset = SelectedProfile;
        var s = _shell.Settings.Current.AgentFor(preset.Id);

        s.ExecutablePath = CliPath.Trim();
        s.CollabModel = CollabModel;
        s.EvalModel = EvalModel;
        s.ModelsCsv = ModelsCsv.Trim();

        s.FirstArgs = FirstArgs.Trim() == preset.FirstArgs.Trim() ? "" : FirstArgs.Trim();
        s.ResumeArgs = ResumeArgs.Trim() == preset.ResumeArgs.Trim() ? "" : ResumeArgs.Trim();
        s.CollabFirstArgs = CollabFirstArgs.Trim() == preset.CollabFirstArgs.Trim() ? "" : CollabFirstArgs.Trim();
        s.CollabResumeArgs = CollabResumeArgs.Trim() == preset.CollabResumeArgs.Trim() ? "" : CollabResumeArgs.Trim();
        s.SessionMode = SessionMode == preset.SessionMode.ToString() ? "" : SessionMode;
        s.PromptVia = PromptVia == preset.PromptVia.ToString() ? "" : PromptVia;
        s.ReplyFormat = ReplyFormat == preset.ReplyFormat.ToString() ? "" : ReplyFormat;
        s.ReplyJsonField = ReplyJsonField.Trim() == preset.ReplyJsonField ? "" : ReplyJsonField.Trim();

        _shell.Settings.Current.SelectedAgentId = preset.Id;
        _shell.Settings.Current.Theme = Theme;
        _shell.Settings.Save();
    }

    private void OpenDataFolder()
    {
        try
        {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + AppPaths.Root + "\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Notice = "폴더를 열지 못했습니다: " + ex.Message;
        }
    }

    private void SaveAndLeave()
    {
        ApplyToSettings();
        _shell.GoHome();
    }

    private static string Shorten(string s)
    {
        s = s.Trim();
        return s.Length > 1500 ? s.Substring(0, 1500) + "\n…(생략)" : s;
    }
}
