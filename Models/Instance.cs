// Models/Instance.cs
using System;
using System.Collections.Generic;
using System.IO; // Required for Path.Combine
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    public class Instance
    {
        public string Id { get; set; } // Unique GUID
        public string Name { get; set; } // User-friendly name
        public string MinecraftVersionId { get; set; } // e.g., "1.20.4"
        public string InstancePath { get; set; } // Full path to the instance directory
        
        [JsonIgnore] 
        public string NativesPath => Path.Combine(InstancePath, "natives");
        
        [JsonIgnore] 
        public string GameDataPath => InstancePath;

        public string CustomJavaRuntimePath { get; set; }
        public List<string> CustomJvmArguments { get; set; }
        public string PlayerName { get; set; } 

        public DateTime CreationDate { get; set; }
        public DateTime LastPlayedDate { get; set; }

        // New playtime properties
        public TimeSpan TotalPlaytime { get; set; }
        public TimeSpan LastSessionPlaytime { get; set; }
        
        public string CustomIconPath { get; set; }

        public Instance()
        {
            Id = Guid.NewGuid().ToString();
            CreationDate = DateTime.UtcNow;
            LastPlayedDate = DateTime.MinValue; // Initialize to indicate never played
            CustomJvmArguments = new List<string>();
            TotalPlaytime = TimeSpan.Zero;
            LastSessionPlaytime = TimeSpan.Zero;
        }
    }
}