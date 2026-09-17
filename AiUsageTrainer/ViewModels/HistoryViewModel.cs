using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using AiUsageTrainer.Common;
using AiUsageTrainer.Models;

namespace AiUsageTrainer.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private List<PracticeReport> _all = new();

    private string _filter = "";
    private PracticeReport? _selected;
    private bool _confirmingDelete;
    private string _notice = "";

    public HistoryViewModel(MainWindowViewModel shell)
    {
        _shell = shell;

        OpenCommand = new RelayCommand(Open, () => Selected is not null);
        DeleteCommand = new RelayCommand(() => ConfirmingDelete = true, () => Selected is not null);
        ConfirmDeleteCommand = new RelayCommand(Delete);
        CancelDeleteCommand = new RelayCommand(() => ConfirmingDelete = false);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        BackCommand = new RelayCommand(_shell.GoHome);
        NewPracticeCommand = new RelayCommand(() => _shell.GoToSetup());

        Reload();
    }

    public ObservableCollection<PracticeReport> Reports { get; } = new();

    public RelayCommand OpenCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand NewPracticeCommand { get; }

    public string Filter
    {
        get => _filter;
        set { if (SetProperty(ref _filter, value)) ApplyFilter(); }
    }

    public PracticeReport? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            ConfirmingDelete = false;
            OpenCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => Selected is not null;

    public bool ConfirmingDelete
    {
        get => _confirmingDelete;
        private set => SetProperty(ref _confirmingDelete, value);
    }

    public string Notice
    {
        get => _notice;
        private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); }
    }

    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    public bool IsEmpty => Reports.Count == 0;

    public string EmptyText => _all.Count == 0
        ? "아직 완료한 연습이 없습니다. 한 번 풀고 나면 여기에 쌓입니다."
        : "검색 조건에 맞는 연습이 없습니다.";

    private void Reload()
    {
        _all = _shell.Reports.LoadAll();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = Filter.Trim();
        var items = q.Length == 0
            ? _all
            : _all.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.DomainTypeLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Evaluation.OverallComment.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        Reports.Clear();
        foreach (var r in items) Reports.Add(r);

        Selected = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void Open()
    {
        if (Selected is null) return;
        _shell.GoToResult(Selected, justFinished: false);
    }

    private void Delete()
    {
        if (Selected is null) return;
        try
        {
            _shell.Reports.Delete(Selected);
            Notice = "연습 기록을 삭제했습니다.";
        }
        catch (Exception ex)
        {
            Notice = "삭제에 실패했습니다: " + ex.Message;
        }
        ConfirmingDelete = false;
        Reload();
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + _shell.Reports.ReportsDirectory + "\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Notice = "폴더를 열지 못했습니다: " + ex.Message;
        }
    }
}
