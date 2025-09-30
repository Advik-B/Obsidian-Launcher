using System;
using ObsidianLauncher.Services;

namespace ObsidianLauncher.Models;

public class InstanceSetupProgress
{
    public string CurrentTask { get; set; } = "";
    public double OverallProgress { get; set; } = 0.0;
    public string Phase { get; set; } = "Starting";
    public AssetDownloadProgress AssetProgress { get; set; } = new();
    public LibraryProcessingProgress LibraryProgress { get; set; } = new();
    public bool IsComplete { get; set; } = false;
    public string ErrorMessage { get; set; } = "";
}