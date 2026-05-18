using Avalonia.Controls;
using Avalonia.Input;

namespace ObsidianLauncher.Views;

public partial class CreateInstanceOverlay : UserControl
{
    public CreateInstanceOverlay()
    {
        InitializeComponent();

        if (this.FindControl<Button>("CreateBtn") is { } btn)
            btn.Click += (_, _) =>
            {
                // For now just close the overlay; full wizard in future iteration
                if (DataContext is ViewModels.MainWindowViewModel vm)
                    vm.CloseCreateInstanceCommand.Execute(null);
            };
    }

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only close if the scrim itself was clicked (not the modal card)
        if (e.Source == sender && DataContext is ViewModels.MainWindowViewModel vm)
            vm.CloseCreateInstanceCommand.Execute(null);
    }
}
