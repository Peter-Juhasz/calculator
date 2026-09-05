using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Calculator.Client.WinUI3;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

        // HACK: fix for sluggish UI on Windows 11, see https://github.com/microsoft/microsoft-ui-xaml/issues/11144
        CompositionTarget.Rendering += (_, _) => { };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
