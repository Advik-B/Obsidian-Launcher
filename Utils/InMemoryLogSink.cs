using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace ObsidianLauncher.Utils;

public sealed class InMemoryLogSink : ILogEventSink
{
    private const int MaxEntries = 10_000;
    private const string OutputTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly ConcurrentQueue<string> _entries = new();
    private readonly MessageTemplateTextFormatter _formatter = new(OutputTemplate, null);

    public static InMemoryLogSink Instance { get; } = new();

    public event Action<string>? EntryAdded;

    private InMemoryLogSink() { }

    public void Emit(LogEvent logEvent)
    {
        var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        var formatted = writer.ToString().TrimEnd();

        _entries.Enqueue(formatted);

        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        EntryAdded?.Invoke(formatted);
    }

    public IReadOnlyList<string> GetEntries() => new List<string>(_entries);
}
