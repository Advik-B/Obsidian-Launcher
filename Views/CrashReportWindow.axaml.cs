using Avalonia.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class CrashReportWindow : Window
{
    public CrashReportWindow() { InitializeComponent(); }

    public CrashReportWindow(CrashReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
