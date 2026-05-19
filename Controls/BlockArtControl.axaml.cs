using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace ObsidianLauncher.Controls;

public partial class BlockArtControl : UserControl
{
    public static readonly StyledProperty<string> PaletteProperty =
        AvaloniaProperty.Register<BlockArtControl, string>(nameof(Palette), "grass");

    public string Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    private static readonly Dictionary<string, (string top, string mid, string bas, string accent, string spot)> Palettes =
        new()
        {
            ["grass"]    = ("#a6c89a", "#7faa70", "#6b4a3e", "#8fb583", "#f5d97a"),
            ["cobble"]   = ("#9c98ad", "#7a7790", "#5b5872", "#b4b0c4", "#cbb6f5"),
            ["diamond"]  = ("#a3d8e8", "#74c0d5", "#3a8aa5", "#c5e8f1", "#ffffff"),
            ["lapis"]    = ("#7287fd", "#5564d8", "#3b48a8", "#b4befe", "#f5d97a"),
            ["nether"]   = ("#c66c5e", "#a3413a", "#6b2620", "#e09084", "#fab387"),
            ["endstone"] = ("#f1e6b8", "#d8c98a", "#a89a5e", "#fdf6d8", "#cba6f7"),
            ["redstone"] = ("#d97a8d", "#b04a60", "#7a2a3e", "#f5b8c0", "#f9e2af"),
            ["oakwood"]  = ("#c9a47a", "#a37a52", "#6b4a30", "#dfc59c", "#a6e3a1"),
            ["mauve"]    = ("#cba6f7", "#a980e0", "#6b4a86", "#ddc4f8", "#f5c2e7"),
            ["cherry"]   = ("#f5c2e7", "#d6a0c8", "#9a6c8c", "#fae3f1", "#a6e3a1"),
        };

    // 9 rows × 16 cols: t=top, m=mid, b=base, a=accent, s=spot
    private static readonly string[] Pattern =
    {
        "tttttttttttttttt",
        "tatttatatttaattt",
        "mmmmmmmmmmmmmmmm",
        "mmamsmmmmsmamsmm",
        "mmmmmsmamsmmmmsm",
        "msmmamsmmmsmmsmm",
        "mmmmmmmsmmmamsmm",
        "bbbbbbbbbbbbbbbb",
        "bbbbabbbbabbbbbb",
    };

    private ObservableCollection<string> _pixels = new();

    public BlockArtControl()
    {
        InitializeComponent();
        UpdatePixels();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaletteProperty)
            UpdatePixels();
    }

    private void UpdatePixels()
    {
        var paletteName = Palette ?? "grass";
        if (!Palettes.TryGetValue(paletteName, out var p))
            p = Palettes["grass"];

        _pixels.Clear();
        foreach (var row in Pattern)
        {
            foreach (var c in row)
            {
                var color = c switch
                {
                    't' => p.top,
                    'm' => p.mid,
                    'b' => p.bas,
                    'a' => p.accent,
                    's' => p.spot,
                    _   => p.mid,
                };
                _pixels.Add(color);
            }
        }

        if (this.FindControl<ItemsControl>("PixelGrid") is { } grid)
            grid.ItemsSource = _pixels;
    }
}
