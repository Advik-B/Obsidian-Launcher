using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace ObsidianLauncher.Utils;

public static class FolderLinker
{
    public static void CreateFolderLink(string linkPath, string targetPath)
    {
        // If the link already exists, do nothing
        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            Log.Information("Link already exists: {LinkPath}", linkPath);
            return;
        }

        if (!Directory.Exists(targetPath))
        {
            Log.Information("Target does not exist. Creating: {TargetPath}", targetPath);
            Directory.CreateDirectory(targetPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Use cmd to make a junction
            string command = $"/C mklink /J \"{linkPath}\" \"{targetPath}\"";
            Process.Start(new ProcessStartInfo("cmd.exe", command)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit();

            Log.Information("Created junction link: {LinkPath} -> {TargetPath}", linkPath, targetPath);
        }
        else
        {
            // Linux or macOS – create symbolic link
            string escapedTarget = targetPath.Replace("'", "'\\''");
            string escapedLink = linkPath.Replace("'", "'\\''");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"ln -s '{escapedTarget}' '{escapedLink}'\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit();

            Log.Information("Created symbolic link: {LinkPath} -> {TargetPath}", linkPath, targetPath);
        }
    }
}