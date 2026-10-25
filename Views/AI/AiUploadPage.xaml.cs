using FlashRevise.ViewModels;

namespace FlashRevise.Views.AI;

[QueryProperty(nameof(ScanRequest), "scan")]
public partial class AiUploadPage : ContentPage
{
    private readonly AiUploadViewModel _viewModel;
    private bool _scanHandled;

    public AiUploadPage(AiUploadViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public string? ScanRequest { get; set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);

        if (!_scanHandled && ScanRequest == "1")
        {
            _scanHandled = true;
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
                _viewModel.ScanPhotoCommand.Execute(null));
        }
    }
}
