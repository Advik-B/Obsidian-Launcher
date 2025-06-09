using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ObsidianLauncher.Enums;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class ArgumentBuilder
{
    private readonly string _authUuid = Guid.NewGuid().ToString("N");
    private readonly LauncherConfig _config; // Still used for assets_root, etc.

    // Launcher settings
    private readonly string _launcherName = "ObsidianLauncher.NET";
    private readonly string _launcherVersion = LauncherConfig.VERSION; // Use actual version
    private readonly ILogger _logger;
    private readonly string _quickPlayMultiplayer = "N/A";
    private readonly string _quickPlayPath = "N/A";
    private readonly string _quickPlayRealms = "N/A";
    private readonly string _quickPlaySingleplayer = "N/A";
    private string _authAccessToken = "0";

    // Offline mode defaults
    private string _authPlayerName = "Player";
    private string _authXuid = "0";
    private string _clientId = "0";
    private bool _hasCustomResolution;

    // Quick Play placeholders
    private bool _hasQuickPlaysSupport;
    private bool _isDemoUser;
    private bool _isQuickPlayMultiplayer;
    private bool _isQuickPlayRealms;
    private bool _isQuickPlaySingleplayer;
    private string _resolutionHeight = "480";
    private string _resolutionWidth = "854";
    private string _userType = "legacy";


    public ArgumentBuilder(LauncherConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = LogHelper.GetLogger<ArgumentBuilder>();
        _logger.Information("ArgumentBuilder initialized for offline mode by default.");
        _logger.Verbose("Default offline auth: PlayerName={PlayerName}, UUID={AuthUuid}, AccessToken={AccessToken}",
            _authPlayerName, _authUuid, _authAccessToken);
    }

    public void SetOfflinePlayerName(string playerName)
    {
        if (!string.IsNullOrWhiteSpace(playerName))
        {
            _authPlayerName = playerName;
            _logger.Information("Offline player name set to: {PlayerName}", _authPlayerName);
        }
        else
        {
            _logger.Warning("Attempted to set empty or null offline player name. Using default: {DefaultPlayerName}",
                _authPlayerName);
        }

        _authAccessToken = "0";
        _userType = "legacy";
        _authXuid = "0";
        _clientId = "0";
    }

    public void SetCustomResolution(int width, int height)
    {
        _resolutionWidth = width.ToString();
        _resolutionHeight = height.ToString();
        _hasCustomResolution = true;
        SetFeatureFlag("has_custom_resolution", true);
        _logger.Information("Custom resolution set: {Width}x{Height}", width, height);
    }

    public void SetFeatureFlag(string featureName, bool value)
    {
        _logger.Verbose("Setting feature flag: {FeatureName} = {Value}", featureName, value);
        if (featureName.Equals("is_demo_user", StringComparison.OrdinalIgnoreCase)) _isDemoUser = value;
        else if (featureName.Equals("has_custom_resolution", StringComparison.OrdinalIgnoreCase))
            _hasCustomResolution = value;
        else if (featureName.Equals("has_quick_plays_support", StringComparison.OrdinalIgnoreCase))
            _hasQuickPlaysSupport = value;
        else if (featureName.Equals("is_quick_play_singleplayer", StringComparison.OrdinalIgnoreCase))
            _isQuickPlaySingleplayer = value;
        else if (featureName.Equals("is_quick_play_multiplayer", StringComparison.OrdinalIgnoreCase))
            _isQuickPlayMultiplayer = value;
        else if (featureName.Equals("is_quick_play_realms", StringComparison.OrdinalIgnoreCase))
            _isQuickPlayRealms = value;
        else _logger.Warning("Attempted to set unknown feature flag for arguments: {FeatureName}", featureName);
    }


    public string BuildClasspath(string clientJarPath, List<string> libraryJarPaths)
    {
        _logger.Information("Building classpath...");
        var allEntries = new List<string>();

        if (libraryJarPaths != null) allEntries.AddRange(libraryJarPaths.Where(p => !string.IsNullOrWhiteSpace(p)));

        if (!string.IsNullOrWhiteSpace(clientJarPath) && File.Exists(clientJarPath))
            allEntries.Add(Path.GetFullPath(clientJarPath));
        else if (!string.IsNullOrWhiteSpace(clientJarPath))
            _logger.Warning("Client JAR path specified for classpath ({ClientJarPath}) but file does not exist.",
                clientJarPath);
        else
            _logger.Warning("Client JAR path was null or empty for classpath construction.");

        var classpathString = string.Join(Path.PathSeparator.ToString(), allEntries.Distinct());
        _logger.Information("Classpath constructed with {Count} entries.", allEntries.Distinct().Count());
        LogPathString("Classpath Preview", classpathString, 500);
        return classpathString;
    }

    public List<string> BuildJvmArguments(
        MinecraftVersion mcVersion,
        string classpath,
        string nativesDir, // This will be instance specific
        JavaRuntimeInfo javaRuntime,
        string instancePath) // New parameter
    {
        _logger.Information("Building JVM arguments for version {VersionId} (Instance: {InstancePath})...",
            mcVersion.Id, instancePath);
        var jvmArgs = new List<string>();

        if (mcVersion.Arguments?.Jvm != null)
        {
            foreach (var argWrapper in mcVersion.Arguments.Jvm)
                if (argWrapper.IsPlainString)
                {
                    jvmArgs.Add(ReplacePlaceholders(argWrapper.PlainStringValue, mcVersion, classpath, nativesDir,
                        instancePath));
                }
                else if (argWrapper.IsConditional)
                {
                    var conditionalArg = argWrapper.ConditionalValue;
                    if (AreRulesSatisfied(conditionalArg.Rules, javaRuntime))
                    {
                        if (conditionalArg.IsSingleValue())
                            jvmArgs.Add(ReplacePlaceholders(conditionalArg.GetSingleValue(), mcVersion, classpath,
                                nativesDir, instancePath));
                        else if (conditionalArg.IsListValue())
                            jvmArgs.AddRange(conditionalArg.GetListValue()
                                .Select(val =>
                                    ReplacePlaceholders(val, mcVersion, classpath, nativesDir, instancePath)));
                    }
                }
        }
        else
        {
            _logger.Information(
                "No modern JVM arguments structure found for {VersionId}. Applying default/legacy JVM arguments.",
                mcVersion.Id);
            jvmArgs.Add($"-Djava.library.path=\"{Path.GetFullPath(nativesDir)}\"");
            jvmArgs.Add("-cp");
            jvmArgs.Add($"\"{classpath}\"");
        }

        if (mcVersion.Logging?.Client?.File != null && !string.IsNullOrEmpty(mcVersion.Logging.Client.Argument))
        {
            var logConfigFileId = mcVersion.Logging.Client.File.Id;
            var logConfigDir = Path.Combine(_config.AssetsDir, "log_configs");
            var logConfigFilePath = Path.Combine(logConfigDir, logConfigFileId);

            if (File.Exists(logConfigFilePath))
            {
                // Pass instancePath to ReplacePlaceholders, though this specific placeholder doesn't use it.
                var loggingArg = ReplacePlaceholders(mcVersion.Logging.Client.Argument, mcVersion, classpath,
                        nativesDir, instancePath)
                    .Replace("${path}", $"\"{Path.GetFullPath(logConfigFilePath)}\"");
                jvmArgs.Add(loggingArg);
                _logger.Information("Added client logging argument: {LoggingArg}", loggingArg);
            }
            else
            {
                _logger.Warning(
                    "Logging configuration file {LogConfigFileId} not found at {LogConfigFilePath}. Logging argument will not be added.",
                    logConfigFileId, logConfigFilePath);
            }
        }

        _logger.Information("JVM arguments built. Count: {Count}", jvmArgs.Count);
        jvmArgs.ForEach(arg => _logger.Verbose("  JVM Arg: {Argument}", arg));
        return jvmArgs;
    }

    public List<string> BuildGameArguments(MinecraftVersion mcVersion, string instancePath) // New parameter
    {
        _logger.Information("Building game arguments for version {VersionId} (Instance: {InstancePath})...",
            mcVersion.Id, instancePath);
        var gameArgs = new List<string>();

        if (mcVersion.Arguments?.Game != null)
        {
            foreach (var argWrapper in mcVersion.Arguments.Game)
                if (argWrapper.IsPlainString)
                {
                    gameArgs.Add(ReplacePlaceholders(argWrapper.PlainStringValue, mcVersion, null, null, instancePath));
                }
                else if (argWrapper.IsConditional)
                {
                    var conditionalArg = argWrapper.ConditionalValue;
                    if (AreRulesSatisfied(conditionalArg.Rules, null))
                    {
                        if (conditionalArg.IsSingleValue())
                            gameArgs.Add(ReplacePlaceholders(conditionalArg.GetSingleValue(), mcVersion, null, null,
                                instancePath));
                        else if (conditionalArg.IsListValue())
                            gameArgs.AddRange(conditionalArg.GetListValue()
                                .Select(val => ReplacePlaceholders(val, mcVersion, null, null, instancePath)));
                    }
                }
        }
        else if (!string.IsNullOrEmpty(mcVersion.MinecraftArguments))
        {
            _logger.Information("Using legacy minecraftArguments string for {VersionId}.", mcVersion.Id);
            var legacyArgsRaw =
                mcVersion.MinecraftArguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            gameArgs.AddRange(
                legacyArgsRaw.Select(arg => ReplacePlaceholders(arg, mcVersion, null, null, instancePath)));
        }
        else
        {
            _logger.Warning(
                "No game arguments found for version {VersionId} (neither modern 'arguments.game' nor legacy 'minecraftArguments').",
                mcVersion.Id);
        }

        _logger.Information("Game arguments built. Count: {Count}", gameArgs.Count);
        gameArgs.ForEach(arg => _logger.Verbose("  Game Arg: {Argument}", arg));
        return gameArgs;
    }

    private string ReplacePlaceholders(string argument, MinecraftVersion mcVersion, string classpath, string nativesDir,
        string instancePath)
    {
        if (argument == null) return null;

        var assetsIndexName = mcVersion.AssetIndex?.Id ?? mcVersion.Assets ?? "unknown_assets_index";

        // Use instancePath for game_directory
        var gameDirectoryPath = $"\"{Path.GetFullPath(instancePath)}\"";
        var assetsRootPath = $"\"{Path.GetFullPath(_config.AssetsDir)}\""; // Assets are global
        var nativesDirectoryPath =
            nativesDir != null
                ? $"\"{Path.GetFullPath(nativesDir)}\""
                : "\"${natives_directory}\""; // Natives are per-instance
        var effectiveClasspath = classpath != null ? $"\"{classpath}\"" : "\"${classpath}\"";

        argument = argument.Replace("${auth_player_name}", _authPlayerName);
        argument = argument.Replace("${auth_uuid}", _authUuid);
        argument = argument.Replace("${auth_access_token}", _authAccessToken);
        argument = argument.Replace("${clientid}", _clientId);
        argument = argument.Replace("${auth_xuid}", _authXuid ?? "0");
        argument = argument.Replace("${user_type}", _userType);

        argument = argument.Replace("${version_name}", mcVersion.Id ?? "unknown_version");
        argument = argument.Replace("${version_type}", mcVersion.Type ?? "unknown_type");

        argument = argument.Replace("${game_directory}", gameDirectoryPath);
        argument = argument.Replace("${game_dir}", gameDirectoryPath);
        argument = argument.Replace("${assets_root}", assetsRootPath);
        argument = argument.Replace("${assets_index_name}", assetsIndexName);

        if (classpath != null)
            argument = argument.Replace("${classpath}", effectiveClasspath);
        if (nativesDir != null)
            argument = argument.Replace("${natives_directory}", nativesDirectoryPath);

        argument = argument.Replace("${launcher_name}", _launcherName);
        argument = argument.Replace("${launcher_version}", _launcherVersion);

        argument = argument.Replace("${resolution_width}", _resolutionWidth);
        argument = argument.Replace("${resolution_height}", _resolutionHeight);

        argument = argument.Replace("${quickPlayPath}", _quickPlayPath);
        argument = argument.Replace("${quickPlaySingleplayer}", _quickPlaySingleplayer);
        argument = argument.Replace("${quickPlayMultiplayer}", _quickPlayMultiplayer);
        argument = argument.Replace("${quickPlayRealms}", _quickPlayRealms);

        return argument;
    }

    private bool AreRulesSatisfied(List<ArgumentRuleCondition> rules, JavaRuntimeInfo javaRuntimeForJvmRules)
    {
        if (rules == null || !rules.Any()) return true;

        var anAllowRuleMatchedAndWasSatisfied = false; // Default: if rules are present, an allow rule must match.

        foreach (var rule in rules)
        {
            var conditionMet = true;

            if (rule.Os != null)
                if (!CheckOsRule(rule.Os, javaRuntimeForJvmRules))
                    conditionMet = false;

            if (rule.Features != null && conditionMet)
                foreach (var featureRule in rule.Features)
                {
                    var currentFeatureState = GetCurrentFeatureState(featureRule.Key);
                    if (currentFeatureState != featureRule.Value)
                    {
                        conditionMet = false;
                        break;
                    }
                }

            if (conditionMet)
            {
                if (rule.Action == RuleAction.Disallow)
                {
                    _logger.Verbose(
                        "Rule matched and DISALLOWED argument due to: OS={RuleOsName}, Features: {Features}",
                        rule.Os?.Name ?? "N/A",
                        rule.Features != null
                            ? string.Join(",", rule.Features.Select(kv => kv.Key + "=" + kv.Value))
                            : "N/A");
                    return false;
                }

                if (rule.Action == RuleAction.Allow)
                {
                    _logger.Verbose("Rule matched and ALLOWED argument due to: OS={RuleOsName}, Features: {Features}",
                        rule.Os?.Name ?? "N/A",
                        rule.Features != null
                            ? string.Join(",", rule.Features.Select(kv => kv.Key + "=" + kv.Value))
                            : "N/A");
                    anAllowRuleMatchedAndWasSatisfied = true;
                }
            }
        }

        return anAllowRuleMatchedAndWasSatisfied;
    }


    private bool CheckOsRule(OperatingSystemInfo osRule, JavaRuntimeInfo javaRuntimeForJvmRules)
    {
        if (osRule == null) return true;

        var nameMatch = true;
        if (!string.IsNullOrEmpty(osRule.Name))
        {
            var currentOs = OsUtils.GetCurrentOS();
            var currentOsName = "";
            switch (currentOs)
            {
                case OperatingSystemType.Windows: currentOsName = "windows"; break;
                case OperatingSystemType.MacOS: currentOsName = "osx"; break;
                case OperatingSystemType.Linux: currentOsName = "linux"; break;
                default:
                    _logger.Warning("CheckOsRule: Unknown current OS type {CurrentOs}", currentOs);
                    return false;
            }

            nameMatch = osRule.Name.Equals(currentOsName, StringComparison.OrdinalIgnoreCase);
            if (!nameMatch) return false;
        }

        if (!string.IsNullOrEmpty(osRule.Version))
            _logger.Verbose(
                "OS version rule found ('{RuleVersion}') for OS '{OsName}'. Version regex matching is complex and not fully implemented here. Assuming pass for now.",
                osRule.Version, osRule.Name ?? "Any");

        if (!string.IsNullOrEmpty(osRule.Arch))
        {
            var currentHostArch = OsUtils.GetCurrentArchitecture();
            var currentHostArchString = "";
            switch (currentHostArch)
            {
                case ArchitectureType.X86: currentHostArchString = "x86"; break;
                case ArchitectureType.X64: currentHostArchString = "x64"; break;
                case ArchitectureType.Arm: currentHostArchString = "arm"; break;
                case ArchitectureType.Arm64: currentHostArchString = "arm64"; break;
                default:
                    _logger.Warning("CheckOsRule: Unknown current host architecture {CurrentArch}", currentHostArch);
                    return false;
            }

            if (!osRule.Arch.Equals(currentHostArchString, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Verbose(
                    "OS arch mismatch for rule. Rule Arch: {RuleArch}, Current Host Arch: {CurrentHostArch}",
                    osRule.Arch, currentHostArchString);
                return false;
            }
        }

        return true;
    }

    private bool GetCurrentFeatureState(string featureName)
    {
        if (featureName.Equals("is_demo_user", StringComparison.OrdinalIgnoreCase)) return _isDemoUser;
        if (featureName.Equals("has_custom_resolution", StringComparison.OrdinalIgnoreCase))
            return _hasCustomResolution;
        if (featureName.Equals("has_quick_plays_support", StringComparison.OrdinalIgnoreCase))
            return _hasQuickPlaysSupport;
        if (featureName.Equals("is_quick_play_singleplayer", StringComparison.OrdinalIgnoreCase))
            return _isQuickPlaySingleplayer;
        if (featureName.Equals("is_quick_play_multiplayer", StringComparison.OrdinalIgnoreCase))
            return _isQuickPlayMultiplayer;
        if (featureName.Equals("is_quick_play_realms", StringComparison.OrdinalIgnoreCase)) return _isQuickPlayRealms;

        _logger.Warning("Checked unknown feature in rule evaluation: {FeatureName}. Assuming 'false'.", featureName);
        return false;
    }

    private void LogPathString(string prefix, string pathString, int previewLength)
    {
        if (pathString.Length > previewLength)
            _logger.Verbose("{Prefix} (first {Length} chars): {PathPreview}...", prefix, previewLength,
                pathString.Substring(0, previewLength));
        else
            _logger.Verbose("{Prefix}: {PathString}", prefix, pathString);
    }
}