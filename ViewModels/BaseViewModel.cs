using CommunityToolkit.Mvvm.ComponentModel;

namespace FlashRevise.ViewModels;

/// <summary>Base commune : état d'occupation, erreurs visibles et connectivité.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsOffline { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    protected void ClearError() => ErrorMessage = null;

    protected void ReportError(Exception exception)
    {
        ErrorMessage = exception.Message;
    }
}
