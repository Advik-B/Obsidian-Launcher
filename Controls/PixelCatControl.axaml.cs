using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace ObsidianLauncher.Controls;

public partial class PixelCatControl : UserControl
{
    public static readonly StyledProperty<double> CatSizeProperty =
        AvaloniaProperty.Register<PixelCatControl, double>(nameof(CatSize), 64.0);

    public double CatSize
    {
        get => GetValue(CatSizeProperty);
        set => SetValue(CatSizeProperty, value);
    }

    // 12×10 pixel cat pattern
    // p=#8839ef(primary), c=#cba6f7(primary-soft), e=#ffffff, m=#ea76cb(secondary)
    private static readonly string[] CatPixels =
    {
        "............",
        "..pp....pp..",
        ".pcpp..ppcp.",
        ".pccpppccpp.",
        ".pccccccccp.",
        ".pcecccccep.",
        "..pccmcccp..",
        "..pcccccp...",
        "...pppp.....",
        "............",
    };

    private static readonly IBrush PBrush = new SolidColorBrush(Color.Parse("#8839ef"));
    private static readonly IBrush CBrush = new SolidColorBrush(Color.Parse("#cba6f7"));
    private static readonly IBrush EBrush = Brushes.White;
    private static readonly IBrush MBrush = new SolidColorBrush(Color.Parse("#ea76cb"));

    public PixelCatControl()
    {
        InitializeComponent();
        DrawCat();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CatSizeProperty)
            DrawCat();
    }

    private void DrawCat()
    {
        if (this.FindControl<Canvas>("CatCanvas") is not { } canvas)
            return;

        canvas.Children.Clear();

        var size = CatSize;
        var cell = size / 12.0;
        var height = size * (10.0 / 12.0);

        canvas.Width = size;
        canvas.Height = height;
        Width = size;
        Height = height;

        for (int y = 0; y < CatPixels.Length; y++)
        {
            var row = CatPixels[y];
            for (int x = 0; x < row.Length; x++)
            {
                var ch = row[x];
                if (ch == '.') continue;

                IBrush brush = ch switch
                {
                    'p' => PBrush,
                    'c' => CBrush,
                    'e' => EBrush,
                    'm' => MBrush,
                    _   => PBrush,
                };

                var rect = new Rectangle
                {
                    Width = cell,
                    Height = cell,
                    Fill = brush,
                };
                Canvas.SetLeft(rect, x * cell);
                Canvas.SetTop(rect, y * cell);
                canvas.Children.Add(rect);
            }
        }
    }
}
