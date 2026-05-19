using System;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace SleepyCat;

/// <summary>
/// Sleepy Cat design system theme for Avalonia 12.
///
/// Usage in App.axaml:
///   xmlns:sleepycat="clr-namespace:SleepyCat;assembly=SleepyCat"
///   &lt;sleepycat:SleepyCatTheme/&gt;
///
/// Or via StyleInclude (no C# dependency):
///   &lt;StyleInclude Source="avares://SleepyCat/Styles/SleepyCatTheme.axaml"/&gt;
/// </summary>
public class SleepyCatTheme : Styles
{
    private static readonly Uri ThemeUri =
        new Uri("avares://SleepyCat/Styles/SleepyCatTheme.axaml");

    public SleepyCatTheme(IServiceProvider? serviceProvider = null)
    {
        Add(new StyleInclude(baseUri: null) { Source = ThemeUri });
    }
}
