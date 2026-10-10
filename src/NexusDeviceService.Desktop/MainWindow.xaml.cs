using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NexusDeviceService.Desktop.ViewModels;

namespace NexusDeviceService.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml with custom modern window caption controls
/// and ViewModel binding.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Geometry MaximizeGeometry = Geometry.Parse("M 0.5,0.5 L 9.5,0.5 L 9.5,9.5 L 0.5,9.5 Z");
    private static readonly Geometry RestoreGeometry = Geometry.Parse("M 0.5,2.5 L 7.5,2.5 L 7.5,9.5 L 0.5,9.5 Z M 2.5,2.5 L 2.5,0.5 L 9.5,0.5 L 9.5,7.5 L 7.5,7.5");

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            MaximizeIcon.Data = RestoreGeometry;
            BtnMaximizeRestore.ToolTip = "Restore Down";
        }
        else
        {
            MaximizeIcon.Data = MaximizeGeometry;
            BtnMaximizeRestore.ToolTip = "Maximize";
        }
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}