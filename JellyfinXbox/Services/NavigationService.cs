using Windows.UI.Xaml.Controls;

namespace JellyfinXbox.Services;

public class NavigationService
{
    private Frame? _frame;
    private readonly DiagnosticsLogService _diagnostics;

    public NavigationService(DiagnosticsLogService diagnostics) => _diagnostics = diagnostics;

    public void Initialize(Frame frame) => _frame = frame;

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        if (_frame == null) return;
        _diagnostics.Log("navigation", "navigate",
            ("page", pageType.Name),
            ("parameterType", parameter?.GetType().Name ?? "none"));
        _frame.Navigate(pageType, parameter);
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
        {
            _diagnostics.Log("navigation", "back");
            _frame.GoBack();
        }
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;
}
