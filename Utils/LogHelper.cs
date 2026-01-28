using Serilog;

namespace ObsidianLauncher.Utils;

public static class LogHelper
{
    public static ILogger GetLogger<T>()
    {
        return Log.ForContext("SourceContext", typeof(T).Name);
    }
}