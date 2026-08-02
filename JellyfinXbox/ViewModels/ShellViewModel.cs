using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JellyfinXbox.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly Services.NavigationService _nav;

    public ShellViewModel(Services.NavigationService nav)
    {
        _nav = nav;
    }

    [RelayCommand]
    private void NavigateHome() => _nav.NavigateTo(typeof(Views.HomePage));

    [RelayCommand]
    private void NavigateSearch() => _nav.NavigateTo(typeof(Views.SearchPage));
}
