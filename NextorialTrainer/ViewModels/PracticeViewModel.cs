using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NextorialTrainer.Common;
using NextorialTrainer.Models;
using NextorialTrainer.Services;

namespace NextorialTrainer.ViewModels;

public sealed class PracticeViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowViewModel _shell;
    private readonly PracticeSession _session;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _cts;

    private string _draft = "";
    private bool _isBusy;
    private bool _isLoadingProblem = true;
    private string _busyText = "";
    private string _error = "";
    private bool _isFinished;
    private bool _confirmingEnd;
    private bool _confirmingLeave;
    private bool _showProblem = true;

    public PracticeViewModel(MainWindowViewModel shell, PracticeConfig config)
    {
        _shell = shell;
        Config = config;
        _session = new PracticeSession(shell.Backend, config);

        SendCommand = new AsyncRelayCommand(SendAsync, () => CanInteract && Draft.Trim().Length > 0);
        EndCommand = new RelayCommand(() => ConfirmingEnd = true, () => CanInteract && Messages.Any(m => m.IsUser));
        ConfirmEndCommand = new AsyncRelayCommand(() => FinishAsync(endedEarly: true));
        CancelEndCommand = new RelayCommand(() => ConfirmingEnd = false);
        LeaveCommand = new RelayCommand(() => ConfirmingLeave = true);
        ConfirmLeaveCommand = new RelayCommand(LeaveWithoutSaving);
        CancelLeaveCommand = new RelayCommand(() => ConfirmingLeave = false);
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => HasError && !IsBusy);
        ToggleProblemCommand = new RelayCommand(() => ShowProblem = !ShowProblem);
        OpenWorkspaceCommand = new RelayCommand(OpenWorkspace);
        RefreshFilesCommand = new RelayCommand(RefreshFiles);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnPropertyChanged(nameof(ElapsedLabel));
        _timer.Start();

        _ = LoadProblemAsync();
    }

    public PracticeConfig Config { get; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<WorkspaceFileInfo> Files { get; } = new();

    public AsyncRelayCommand SendCommand { get; }
    public RelayCommand EndCommand { get; }
    public AsyncRelayCommand ConfirmEndCommand { get; }
    public RelayCommand CancelEndCommand { get; }
    public RelayCommand LeaveCommand { get; }
    public RelayCommand ConfirmLeaveCommand { get; }
    public RelayCommand CancelLeaveCommand { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public RelayCommand ToggleProblemCommand { get; }
    public RelayCommand OpenWorkspaceCommand { get; }
    public RelayCommand RefreshFilesCommand { get; }

    /// <summary>Raised when a new message lands, so the view can scroll to the bottom.</summary>
    public event EventHandler? MessageAppended;

    public bool IsLoadingProblem
    {
        get => _isLoadingProblem;
        private set { if (SetProperty(ref _isLoadingProblem, value)) OnPropertyChanged(nameof(CanInteract)); }
    }

    public ProblemBrief Problem => _session.Problem;

    public string HeaderLabel => $"{Config.PlannedDomain} · {(ProblemTypeCatalog.ByKey(Config.PlannedTypeKey)?.Name ?? Config.PlannedTypeKey)}";

    public bool ShowProblem
    {
        get => _showProblem;
        private set { if (SetProperty(ref _showProblem, value)) OnPropertyChanged(nameof(ProblemToggleText)); }
    }

    public string ProblemToggleText => ShowProblem ? "문제 접기" : "문제 다시 보기";

    public string Draft
    {
        get => _draft;
        set { if (SetProperty(ref _draft, value)) SendCommand.RaiseCanExecuteChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanInteract));
            RaiseAll();
        }
    }

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    public bool IsFinished
    {
        get => _isFinished;
        private set
        {
            if (!SetProperty(ref _isFinished, value)) return;
            OnPropertyChanged(nameof(CanInteract));
            RaiseAll();
        }
    }

    public bool CanInteract => !IsBusy && !IsFinished && !IsLoadingProblem;

    public string Error
    {
        get => _error;
        private set
        {
            if (!SetProperty(ref _error, value)) return;
            OnPropertyChanged(nameof(HasError));
            RetryCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool ConfirmingEnd
    {
        get => _confirmingEnd;
        private set => SetProperty(ref _confirmingEnd, value);
    }

    public bool ConfirmingLeave
    {
        get => _confirmingLeave;
        private set => SetProperty(ref _confirmingLeave, value);
    }

    public string ElapsedLabel
    {
        get
        {
            var t = _clock.Elapsed;
            var baseLabel = t.Hours > 0
                ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes:D2}:{t.Seconds:D2}";

            if (!Config.UseTargetTime) return baseLabel;
            var remain = TimeSpan.FromMinutes(Config.TargetMinutes) - t;
            var remainLabel = remain.Ticks > 0
                ? $"{(int)remain.TotalMinutes:D2}:{remain.Seconds:D2}"
                : "초과";
            return $"{baseLabel} (목표까지 {remainLabel})";
        }
    }

    private async Task LoadProblemAsync()
    {
        IsLoadingProblem = true;
        BusyText = "문제를 준비하는 중...";

        try
        {
            await _session.GenerateProblemAsync(CancellationToken.None).ConfigureAwait(true);
            OnPropertyChanged(nameof(Problem));
            AddSystem("문제가 준비되었습니다. 아래 입력창에 메시지를 보내 협업 AI와 함께 풀어보세요. Ctrl+Enter로 전송합니다.");
            RefreshFiles();
        }
        catch (AgentCliException ex)
        {
            Error = "문제를 준비하지 못했습니다. " + ex.Message + "\n" + Shorten(ex.Detail);
        }
        catch (Exception ex)
        {
            Error = "문제 준비 중 오류: " + ex.Message;
        }
        finally
        {
            IsLoadingProblem = false;
        }
    }

    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (text.Length == 0) return;

        Draft = "";
        Add(new ChatMessage { Role = ChatRole.User, Text = text });

        Error = "";
        IsBusy = true;
        BusyText = "협업 AI가 응답하는 중...";

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var reply = await _session.SendAsync(text, _cts.Token).ConfigureAwait(true);
            Add(new ChatMessage { Role = ChatRole.Collaborator, Text = reply });
            RefreshFiles();
        }
        catch (OperationCanceledException)
        {
            Error = "요청이 취소되었습니다.";
        }
        catch (AgentCliException ex)
        {
            Error = ex.Message + (string.IsNullOrWhiteSpace(ex.Detail) ? "" : "\n" + Shorten(ex.Detail));
        }
        catch (Exception ex)
        {
            Error = "예기치 못한 오류: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RetryAsync()
    {
        Error = "";
        var last = Messages.LastOrDefault(m => m.IsUser);
        if (last is null) return;
        await SendInternalAsync(last.Text + "\n\n(직전 응답이 전달되지 않았습니다. 다시 답해주세요.)");
    }

    private async Task SendInternalAsync(string text)
    {
        IsBusy = true;
        BusyText = "다시 시도하는 중...";
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var reply = await _session.SendAsync(text, _cts.Token).ConfigureAwait(true);
            Add(new ChatMessage { Role = ChatRole.Collaborator, Text = reply });
            RefreshFiles();
        }
        catch (AgentCliException ex)
        {
            Error = ex.Message + (string.IsNullOrWhiteSpace(ex.Detail) ? "" : "\n" + Shorten(ex.Detail));
        }
        catch (Exception ex)
        {
            Error = "예기치 못한 오류: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FinishAsync(bool endedEarly)
    {
        ConfirmingEnd = false;
        IsFinished = true;
        IsBusy = true;
        BusyText = "제출 완료. 평가지를 작성하는 중... (1분 정도 걸릴 수 있습니다)";
        AddSystem("제출했습니다. 평가지를 작성하고 있습니다.");

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var elapsedLabel = _clock.Elapsed.TotalMinutes >= 60
                ? $"{(int)_clock.Elapsed.TotalHours}시간 {_clock.Elapsed.Minutes}분"
                : $"{(int)_clock.Elapsed.TotalMinutes}분 {_clock.Elapsed.Seconds}초";

            var (evaluation, raw) = await _session
                .EvaluateAsync(Messages.ToList(), elapsedLabel, endedEarly, _cts.Token)
                .ConfigureAwait(true);

            _clock.Stop();
            _timer.Stop();

            var report = new PracticeReport
            {
                Config = Config,
                Problem = _session.Problem,
                Evaluation = evaluation,
                Transcript = new List<ChatMessage>(Messages),
                WorkspacePath = _session.WorkspacePath,
                WorkspaceFiles = WorkspaceManager.ListFiles(_session.WorkspacePath).Select(f => f.RelativePath).ToList(),
                DurationSeconds = (int)_clock.Elapsed.TotalSeconds,
                EndedEarly = endedEarly,
                RawEvaluation = raw
            };

            _shell.Reports.Save(report);
            _shell.GoToResult(report, justFinished: true);
        }
        catch (AgentCliException ex)
        {
            Error = "평가지 작성에 실패했습니다. " + ex.Message + "\n" + Shorten(ex.Detail);
            IsFinished = false;
        }
        catch (Exception ex)
        {
            Error = "평가지 작성 중 오류: " + ex.Message;
            IsFinished = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LeaveWithoutSaving()
    {
        _cts?.Cancel();
        _timer.Stop();

        try
        {
            if (!string.IsNullOrWhiteSpace(_session.WorkspacePath) && System.IO.Directory.Exists(_session.WorkspacePath))
                System.IO.Directory.Delete(_session.WorkspacePath, recursive: true);
        }
        catch (Exception) { /* best effort cleanup */ }

        _shell.GoHome();
    }

    private void OpenWorkspace()
    {
        try { WorkspaceManager.OpenInExplorer(_session.WorkspacePath); }
        catch (Exception ex) { Error = "작업 폴더를 열지 못했습니다: " + ex.Message; }
    }

    private void RefreshFiles()
    {
        Files.Clear();
        foreach (var f in WorkspaceManager.ListFiles(_session.WorkspacePath)) Files.Add(f);
        OnPropertyChanged(nameof(HasFiles));
    }

    public bool HasFiles => Files.Count > 0;

    private void Add(ChatMessage m)
    {
        Messages.Add(m);
        MessageAppended?.Invoke(this, EventArgs.Empty);
        EndCommand.RaiseCanExecuteChanged();
    }

    private void AddSystem(string text) => Add(new ChatMessage { Role = ChatRole.System, Text = text });

    private void RaiseAll()
    {
        SendCommand.RaiseCanExecuteChanged();
        EndCommand.RaiseCanExecuteChanged();
        RetryCommand.RaiseCanExecuteChanged();
    }

    private static string Shorten(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        return s.Length > 600 ? s.Substring(0, 600) + "..." : s;
    }

    public void Dispose()
    {
        _timer.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
