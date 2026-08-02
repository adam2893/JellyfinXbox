using Windows.UI.Xaml.Controls;

namespace JellyfinXbox.Services;

public class NavigationService
{
    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        if (_frame == null) return;
        _frame.Navigate(pageType, parameter);
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
            _frame.GoBack();
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;
}
