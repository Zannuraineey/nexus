using System.Windows.Controls;
using NexusDeviceService.Desktop.ViewModels.Pages;

namespace NexusDeviceService.Desktop.Views.Pages;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            if (DataContext is DevicesViewModel vm && vm.AllDevices.Count == 0 && !vm.IsLoading)
            {
                _ = vm.RefreshDevicesAsync();
            }
        };
    }
}
