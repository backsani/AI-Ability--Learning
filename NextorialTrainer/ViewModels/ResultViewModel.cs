using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NextorialTrainer.Common;
using NextorialTrainer.Models;
using NextorialTrainer.Services;

namespace NextorialTrainer.ViewModels;

public sealed class ResultViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private string _notice = "";
    private bool _showTranscript;

    public ResultViewModel(MainWindowViewModel shell, PracticeReport report, bool justFinished)
    {
        _shell = shell;
        Report = report;
        JustFinished = justFinished;

        if (justFinished)
            _notice = "평가지가 저장되었습니다. 언제든 [지난 연습]에서 다시 열 수 있습니다.";

        ExportCommand = new RelayCommand(Export);
        OpenReportsFolderCommand = new RelayCommand(OpenReportsFolder);
        OpenWorkspaceCommand = new RelayCommand(OpenWorkspace, () => WorkspaceExists);
        PracticeAgainCommand = new RelayCommand(PracticeAgain);
        HomeCommand = new RelayCommand(_shell.GoHome);
        HistoryCommand = new RelayCommand(_shell.GoToHistory);
        ToggleTranscriptCommand = new RelayCommand(() => ShowTranscript = !ShowTranscript);
    }

    public PracticeReport Report { get; }
    public bool JustFinished { get; }

    public Evaluation Evaluation => Report.Evaluation;
    public List<ProcessStepEval> ProcessSteps => Evaluation.ProcessSteps;
    public List<CriterionEval> Criteria => Evaluation.Criteria;
    public List<string> BannedPhrases => Evaluation.BannedPhrasesUsed;
    public List<string> GoodPrompts => Evaluation.GoodPrompts;
    public List<string> HiddenCriteria => Report.Problem.HiddenAcceptanceCriteria;

    public bool HasBannedPhrases => BannedPhrases.Count > 0;
    public bool HasGoodPrompts => GoodPrompts.Count > 0;
    public bool HasHiddenCriteria => HiddenCriteria.Count > 0;
    public bool HasDeliverableCheck => !string.IsNullOrWhiteSpace(Evaluation.DeliverableCheck);
    public bool HasAdvice => !string.IsNullOrWhiteSpace(Evaluation.FinalAdvice);

    public bool WorkspaceExists => !string.IsNullOrWhiteSpace(Report.WorkspacePath) && Directory.Exists(Report.WorkspacePath);

    public IEnumerable<ChatMessage> Transcript => Report.Transcript.Where(m => m.Role != ChatRole.System);

    public string MetaLine =>
        $"{Report.DateLabel} · {Report.DomainTypeLabel} · {Report.Config.Difficulty} · {Report.DurationLabel}" +
        (Report.EndedEarly ? " · 중도 제출" : "");

    public string Notice
    {
        get => _notice;
        private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); }
    }

    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    public bool ShowTranscript
    {
        get => _showTranscript;
        private set { if (SetProperty(ref _showTranscript, value)) OnPropertyChanged(nameof(TranscriptButtonText)); }
    }

    public string TranscriptButtonText => ShowTranscript ? "대화 전문 접기" : "대화 전문 보기";

    public RelayCommand ExportCommand { get; }
    public RelayCommand OpenReportsFolderCommand { get; }
    public RelayCommand OpenWorkspaceCommand { get; }
    public RelayCommand PracticeAgainCommand { get; }
    public RelayCommand HomeCommand { get; }
    public RelayCommand HistoryCommand { get; }
    public RelayCommand ToggleTranscriptCommand { get; }

    private void Export()
    {
        try
        {
            var name = $"{Report.CreatedAt:yyyyMMdd_HHmmss}_{Sanitize(Report.Title)}.md";
            var path = Path.Combine(_shell.Reports.ReportsDirectory, name);
            File.WriteAllText(path, MarkdownExporter.ToMarkdown(Report, includeTranscript: true), Common.Utf8.NoBom);

            Notice = "마크다운으로 내보냈습니다: " + path;
            Reveal(path);
        }
        catch (Exception ex)
        {
            Notice = "내보내기에 실패했습니다: " + ex.Message;
        }
    }

    private void OpenReportsFolder()
    {
        try { Reveal(_shell.Reports.ReportsDirectory); }
        catch (Exception ex) { Notice = "폴더를 열지 못했습니다: " + ex.Message; }
    }

    private void OpenWorkspace()
    {
        try { WorkspaceManager.OpenInExplorer(Report.WorkspacePath); }
        catch (Exception ex) { Notice = "작업 폴더를 열지 못했습니다: " + ex.Message; }
    }

    private void PracticeAgain() => _shell.GoToSetup(Report.Config.PlannedDomain, Report.Config.PlannedTypeKey);

    private static void Reveal(string path)
    {
        var isFile = File.Exists(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = isFile ? $"/select,\"{path}\"" : $"\"{path}\"",
            UseShellExecute = true
        });
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Length > 60 ? s.Substring(0, 60) : s;
    }
}
