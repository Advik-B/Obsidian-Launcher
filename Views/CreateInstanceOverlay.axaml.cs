using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class CreateInstanceOverlay : UserControl
{
    public CreateInstanceOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.NewInstancePalette))
                    UpdatePaletteHighlight(vm.NewInstancePalette);
            };
    }

    private void Palette_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border b && b.Tag is string palette && DataContext is MainWindowViewModel vm)
        {
            vm.NewInstancePalette = palette;
            UpdatePaletteHighlight(palette);
        }
    }

    private void UpdatePaletteHighlight(string selectedPalette)
    {
        if (this.FindControl<WrapPanel>("PalettePanel") is not { } panel) return;
        foreach (var child in panel.Children)
        {
            if (child is Border b && b.Tag is string palette)
            {
                b.BorderBrush = palette == selectedPalette
                    ? this.TryFindResource("PrimaryBrush", out var res) ? (Avalonia.Media.IBrush?)res : Avalonia.Media.Brushes.Transparent
                    : Avalonia.Media.Brushes.Transparent;
                b.BorderThickness = new Avalonia.Thickness(2);
            }
        }
    }

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == sender && DataContext is MainWindowViewModel vm)
            vm.CloseCreateInstanceCommand.Execute(null);
    }
}
