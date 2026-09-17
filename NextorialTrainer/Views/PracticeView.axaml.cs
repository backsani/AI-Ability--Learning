using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NextorialTrainer.ViewModels;

namespace NextorialTrainer.Views;

public partial class PracticeView : UserControl
{
    private PracticeViewModel? _vm;
    private ScrollViewer? _scroll;
    private bool _pinnedToBottom = true;

    public PracticeView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null) _vm.MessageAppended -= OnMessageAppended;
        _vm = DataContext as PracticeViewModel;
        if (_vm is not null) _vm.MessageAppended += OnMessageAppended;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _scroll = this.FindControl<ScrollViewer>("ChatScroll");
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            _scroll.LayoutUpdated += OnLayoutUpdated;
        }

        this.FindControl<TextBox>("AnswerBox")?.Focus();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll.LayoutUpdated -= OnLayoutUpdated;
        }
        if (_vm is not null) _vm.MessageAppended -= OnMessageAppended;
        _vm?.Dispose();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta == default && e.ViewportDelta == default)
            _pinnedToBottom = IsAtBottom();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pinnedToBottom && !IsAtBottom()) _scroll?.ScrollToEnd();
    }

    private bool IsAtBottom()
        => _scroll is null ||
           _scroll.Offset.Y >= _scroll.Extent.Height - _scroll.Viewport.Height - 1;

    private void OnMessageAppended(object? sender, EventArgs e) => _pinnedToBottom = true;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        if (_vm is null) return;

        if (_vm.SendCommand.CanExecute(null))
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
