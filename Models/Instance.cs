// Models/Instance.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

public class Instance
{
    public Instance()
    {
        Id = Guid.NewGuid().ToString();
        CreationDate = DateTime.UtcNow;
        LastPlayedDate = DateTime.MinValue;
        CustomJvmArguments = new List<string>();
        TotalPlaytime = TimeSpan.Zero;
        LastSessionPlaytime = TimeSpan.Zero;
        Components = new List<Component>();
    }

    public string Id { get; set; }
    public string Name { get; set; }
    
    // Replaced MinecraftVersionId with a list of components
    public List<Component> Components { get; set; }

    [JsonIgnore] public string InstancePath { get; set; }

    [JsonIgnore] public string NativesPath => Path.Combine(InstancePath, "natives");

    [JsonIgnore] public string GameDataPath => InstancePath;

    public string CustomJavaRuntimePath { get; set; }

    public List<string> CustomJvmArguments { get; set; }

    public DateTime CreationDate { get; set; }
    public DateTime LastPlayedDate { get; set; }

    public TimeSpan TotalPlaytime { get; set; }
    public TimeSpan LastSessionPlaytime { get; set; }

    public string CustomIconPath { get; set; }
}