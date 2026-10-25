using System.Collections.Specialized;
using System.ComponentModel;
using FlashRevise.ViewModels;

namespace FlashRevise.Views.AI;

public partial class AiChatPage : ContentPage
{
    private readonly AiChatViewModel _viewModel;

    public AiChatPage(AiChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.Messages.CollectionChanged += OnMessagesChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
        ScrollToEnd();
    }

    protected override void OnDisappearing()
    {
        _viewModel.StopCommand.Execute(null);
        base.OnDisappearing();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ChatBubble>())
            {
                item.PropertyChanged += OnBubbleChanged;
            }
        }

        ScrollToEnd();
    }

    private void OnBubbleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatBubble.Text))
        {
            ScrollToEnd();
        }
    }

    private void ScrollToEnd()
    {
        var last = _viewModel.Messages.LastOrDefault();
        if (last is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
            MessagesView.ScrollTo(last, position: ScrollToPosition.End, animate: false));
    }
}
