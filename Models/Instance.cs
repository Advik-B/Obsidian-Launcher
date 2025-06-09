using System;
using System.Collections.Generic;
using System.IO; 
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    public class Instance
    {
        public string Id { get; set; } 
        public string Name { get; set; } 
        public string MinecraftVersionId { get; set; } 
        public string InstancePath { get; set; } 
        
        [JsonIgnore] 
        public string NativesPath => Path.Combine(InstancePath, "natives");
        
        [JsonIgnore] 
        public string GameDataPath => InstancePath;

        public string CustomJavaRuntimePath { get; set; }
        public List<string> CustomJvmArguments { get; set; }
        // PlayerName removed from instance metadata

        public DateTime CreationDate { get; set; }
        public DateTime LastPlayedDate { get; set; }

        public TimeSpan TotalPlaytime { get; set; }
        public TimeSpan LastSessionPlaytime { get; set; }
        
        public string CustomIconPath { get; set; }

        public Instance()
        {
            Id = Guid.NewGuid().ToString();
            CreationDate = DateTime.UtcNow;
            LastPlayedDate = DateTime.MinValue; 
            CustomJvmArguments = new List<string>();
            TotalPlaytime = TimeSpan.Zero;
            LastSessionPlaytime = TimeSpan.Zero;
        }
    }
}