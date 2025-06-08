using Serilog;

namespace ObsidianLauncher.Utils;

public static class LogHelper
{
    public static ILogger GetLogger<T>() =>
        Log.ForContext("SourceContext", typeof(T).Name);
}