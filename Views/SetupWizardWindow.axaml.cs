using System;
using Avalonia.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class SetupWizardWindow : Window
{
    public SetupWizardWindow() { InitializeComponent(); }

    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.WizardCompleted += OnWizardCompleted;
    }

    private void OnWizardCompleted(object? sender, EventArgs e) => Close(true);
}
