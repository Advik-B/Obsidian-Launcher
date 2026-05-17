using System;
using System.Collections.Generic;

namespace ObsidianLauncher.Models;

public class CrashReport
{
    public int ExitCode { get; init; }
    public string InstanceName { get; init; } = "";
    public DateTime CrashedAt { get; init; } = DateTime.UtcNow;
    public List<string> RelevantLines { get; init; } = new();
    public CrashCategory Category { get; init; } = CrashCategory.Unknown;
    public string? Suggestion { get; init; }
}

public enum CrashCategory
{
    Unknown,
    OutOfMemory,
    JvmCrash,
    ModConflict,
    MissingDependency,
    GameCrash
}
