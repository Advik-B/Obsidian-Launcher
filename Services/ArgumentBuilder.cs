// Services/ArgumentBuilder.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions; // For regex in version parsing if needed by rules
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using ObsidianLauncher.Enums;
using Serilog;

namespace ObsidianLauncher.Services
{
    public class ArgumentBuilder
    {
        private readonly LauncherConfig _config;
        private readonly ILogger _logger;

        // Offline mode defaults
        private string _authPlayerName = "Player"; // Default offline player name
        private string _authUuid = Guid.NewGuid().ToString("N"); // Generate a random UUID for offline
        private string _authAccessToken = "0"; // Common placeholder for offline/invalid token
        private string _clientId = "0"; // Placeholder
        private string _authXuid = "0"; // Placeholder
        private string _userType = "legacy"; // Or "offline" if a specific type is expected by some arg processors

        // These might come from launcher settings
        private string _launcherName = "ObsidianLauncher.NET";
        private string _launcherVersion = "0.1";
        private string _resolutionWidth = "854";
        private string _resolutionHeight = "480";
        private bool _hasCustomResolution = false;
        private bool _isDemoUser = false;

        // Quick Play (example placeholders)
        private bool _hasQuickPlaysSupport = false;
        private string _quickPlayPath = "N/A"; // Placeholder if not used
        private string _quickPlaySingleplayer = "N/A";
        private string _quickPlayMultiplayer = "N/A";
        private string _quickPlayRealms = "N/A";
        private bool _isQuickPlaySingleplayer = false;
        private bool _isQuickPlayMultiplayer = false;
        private bool _isQuickPlayRealms = false;


        public ArgumentBuilder(LauncherConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = LogHelper.GetLogger<ArgumentBuilder>();
            _logger.Information("ArgumentBuilder initialized for offline mode by default.");
            // Log the default offline auth info being used
            _logger.Verbose("Default offline auth: PlayerName={PlayerName}, UUID={AuthUuid}, AccessToken={AccessToken}",
                _authPlayerName, _authUuid, _authAccessToken);
        }

        /// <summary>
        /// Sets player name for offline mode. UUID and AccessToken will remain defaults for offline.
        /// </summary>
        public void SetOfflinePlayerName(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                _authPlayerName = playerName;
                 _logger.Information("Offline player name set to: {PlayerName}", _authPlayerName);
            }
            else
            {
                _logger.Warning("Attempted to set empty or null offline player name. Using default: {DefaultPlayerName}", _authPlayerName);
            }
            // For truly offline, other auth fields remain as their default offline placeholders
            _authAccessToken = "0"; // Ensure it's the offline token
            _userType = "legacy"; // Common for offline mode representation
            _authXuid = "0";
            _clientId = "0";
        }


        /// <summary>
        /// Sets custom resolution for game arguments.
        /// </summary>
        public void SetCustomResolution(int width, int height)
        {
            _resolutionWidth = width.ToString();
            _resolutionHeight = height.ToString();
            _hasCustomResolution = true; // This flag is important for the rule
            SetFeatureFlag("has_custom_resolution", true); // Also update the feature flag
            _logger.Information("Custom resolution set: {Width}x{Height}", width, height);
        }

        /// <summary>
        /// Sets a specific feature flag for rule evaluation.
        /// </summary>
        public void SetFeatureFlag(string featureName, bool value)
        {
            _logger.Verbose("Setting feature flag: {FeatureName} = {Value}", featureName, value);
            if (featureName.Equals("is_demo_user", StringComparison.OrdinalIgnoreCase)) _isDemoUser = value;
            else if (featureName.Equals("has_custom_resolution", StringComparison.OrdinalIgnoreCase)) _hasCustomResolution = value;
            else if (featureName.Equals("has_quick_plays_support", StringComparison.OrdinalIgnoreCase)) _hasQuickPlaysSupport = value;
            else if (featureName.Equals("is_quick_play_singleplayer", StringComparison.OrdinalIgnoreCase)) _isQuickPlaySingleplayer = value;
            else if (featureName.Equals("is_quick_play_multiplayer", StringComparison.OrdinalIgnoreCase)) _isQuickPlayMultiplayer = value;
            else if (featureName.Equals("is_quick_play_realms", StringComparison.OrdinalIgnoreCase)) _isQuickPlayRealms = value;
            else _logger.Warning("Attempted to set unknown feature flag for arguments: {FeatureName}", featureName);
        }


        public string BuildClasspath(string clientJarPath, List<string> libraryJarPaths)
        {
            _logger.Information("Building classpath...");
            var allEntries = new List<string>();

            if (libraryJarPaths != null)
            {
                allEntries.AddRange(libraryJarPaths.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            if (!string.IsNullOrWhiteSpace(clientJarPath) && File.Exists(clientJarPath))
            {
                allEntries.Add(Path.GetFullPath(clientJarPath));
            }
            else if (!string.IsNullOrWhiteSpace(clientJarPath))
            {
                _logger.Warning("Client JAR path specified for classpath ({ClientJarPath}) but file does not exist.", clientJarPath);
            }
            else
            {
                _logger.Warning("Client JAR path was null or empty for classpath construction.");
            }

            string classpathString = string.Join(Path.PathSeparator.ToString(), allEntries.Distinct());
            _logger.Information("Classpath constructed with {Count} entries.", allEntries.Distinct().Count());
            LogPathString("Classpath Preview", classpathString, 500);
            return classpathString;
        }

        public List<string> BuildJvmArguments(
            MinecraftVersion mcVersion,
            string classpath,
            string nativesDir,
            JavaRuntimeInfo javaRuntime)
        {
            _logger.Information("Building JVM arguments for version {VersionId}...", mcVersion.Id);
            var jvmArgs = new List<string>();

            if (mcVersion.Arguments?.Jvm != null)
            {
                foreach (var argWrapper in mcVersion.Arguments.Jvm)
                {
                    if (argWrapper.IsPlainString)
                    {
                        jvmArgs.Add(ReplacePlaceholders(argWrapper.PlainStringValue, mcVersion, classpath, nativesDir));
                    }
                    else if (argWrapper.IsConditional)
                    {
                        var conditionalArg = argWrapper.ConditionalValue;
                        if (AreRulesSatisfied(conditionalArg.Rules, javaRuntime))
                        {
                            if (conditionalArg.IsSingleValue())
                            {
                                jvmArgs.Add(ReplacePlaceholders(conditionalArg.GetSingleValue(), mcVersion, classpath, nativesDir));
                            }
                            else if (conditionalArg.IsListValue())
                            {
                                jvmArgs.AddRange(conditionalArg.GetListValue()
                                    .Select(val => ReplacePlaceholders(val, mcVersion, classpath, nativesDir)));
                            }
                        }
                    }
                }
            }
            else
            {
                _logger.Information("No modern JVM arguments structure found for {VersionId}. Applying default/legacy JVM arguments.", mcVersion.Id);
                // Add some very basic default JVM args for older versions if they don't specify any
                jvmArgs.Add($"-Djava.library.path=\"{Path.GetFullPath(nativesDir)}\""); // Essential, and quote path
                jvmArgs.Add("-cp");
                jvmArgs.Add($"\"{classpath}\""); // Quote classpath
            }

            if (mcVersion.Logging?.Client?.File != null && !string.IsNullOrEmpty(mcVersion.Logging.Client.Argument))
            {
                string logConfigFileId = mcVersion.Logging.Client.File.Id;
                // Assuming log configs are downloaded to a specific, known subdirectory by AssetManager or similar
                string logConfigDir = Path.Combine(_config.AssetsDir, "log_configs"); // Or _config.AssetIndexesDir if they are there
                string logConfigFilePath = Path.Combine(logConfigDir, logConfigFileId);

                if (File.Exists(logConfigFilePath))
                {
                    string loggingArg = mcVersion.Logging.Client.Argument.Replace("${path}", $"\"{Path.GetFullPath(logConfigFilePath)}\""); // Quote path
                    jvmArgs.Add(loggingArg);
                    _logger.Information("Added client logging argument: {LoggingArg}", loggingArg);
                }
                else
                {
                    _logger.Warning("Logging configuration file {LogConfigFileId} not found at {LogConfigFilePath}. Logging argument will not be added.",
                        logConfigFileId, logConfigFilePath);
                }
            }

            _logger.Information("JVM arguments built. Count: {Count}", jvmArgs.Count);
            jvmArgs.ForEach(arg => _logger.Verbose("  JVM Arg: {Argument}", arg));
            return jvmArgs;
        }

        public List<string> BuildGameArguments(MinecraftVersion mcVersion)
        {
            _logger.Information("Building game arguments for version {VersionId}...", mcVersion.Id);
            var gameArgs = new List<string>();

            if (mcVersion.Arguments?.Game != null)
            {
                foreach (var argWrapper in mcVersion.Arguments.Game)
                {
                    if (argWrapper.IsPlainString)
                    {
                        gameArgs.Add(ReplacePlaceholders(argWrapper.PlainStringValue, mcVersion, null, null));
                    }
                    else if (argWrapper.IsConditional)
                    {
                        var conditionalArg = argWrapper.ConditionalValue;
                        if (AreRulesSatisfied(conditionalArg.Rules, null))
                        {
                            if (conditionalArg.IsSingleValue())
                            {
                                gameArgs.Add(ReplacePlaceholders(conditionalArg.GetSingleValue(), mcVersion, null, null));
                            }
                            else if (conditionalArg.IsListValue())
                            {
                                gameArgs.AddRange(conditionalArg.GetListValue()
                                    .Select(val => ReplacePlaceholders(val, mcVersion, null, null)));
                            }
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(mcVersion.MinecraftArguments))
            {
                _logger.Information("Using legacy minecraftArguments string for {VersionId}.", mcVersion.Id);
                var legacyArgsRaw = mcVersion.MinecraftArguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                gameArgs.AddRange(legacyArgsRaw.Select(arg => ReplacePlaceholders(arg, mcVersion, null, null)));
            }
            else
            {
                _logger.Warning("No game arguments found for version {VersionId} (neither modern 'arguments.game' nor legacy 'minecraftArguments').", mcVersion.Id);
            }

            _logger.Information("Game arguments built. Count: {Count}", gameArgs.Count);
            gameArgs.ForEach(arg => _logger.Verbose("  Game Arg: {Argument}", arg));
            return gameArgs;
        }

        private string ReplacePlaceholders(string argument, MinecraftVersion mcVersion, string classpath, string nativesDir)
        {
            if (argument == null) return null;

            // Safe navigation for potentially null sub-objects
            string assetsIndexName = mcVersion.AssetIndex?.Id ?? mcVersion.Assets ?? "unknown_assets_index";

            // Ensure paths are full and use quotes for robustness if they might contain spaces
            string gameDirectoryPath = $"\"{Path.GetFullPath(_config.BaseDataPath)}\"";
            string assetsRootPath = $"\"{Path.GetFullPath(_config.AssetsDir)}\"";
            string nativesDirectoryPath = nativesDir != null ? $"\"{Path.GetFullPath(nativesDir)}\"" : "\"${natives_directory}\""; // Keep placeholder if null
            string effectiveClasspath = classpath != null ? $"\"{classpath}\"" : "\"${classpath}\""; // Quote classpath

            argument = argument.Replace("${auth_player_name}", _authPlayerName); // Already a simple string
            argument = argument.Replace("${auth_uuid}", _authUuid);
            argument = argument.Replace("${auth_access_token}", _authAccessToken);
            argument = argument.Replace("${clientid}", _clientId);
            argument = argument.Replace("${auth_xuid}", _authXuid ?? "0"); // Default if null
            argument = argument.Replace("${user_type}", _userType);

            argument = argument.Replace("${version_name}", mcVersion.Id ?? "unknown_version");
            argument = argument.Replace("${version_type}", mcVersion.Type ?? "unknown_type");

            argument = argument.Replace("${game_directory}", gameDirectoryPath);
            argument = argument.Replace("${game_dir}", gameDirectoryPath); // Some older versions might use this
            argument = argument.Replace("${assets_root}", assetsRootPath);
            argument = argument.Replace("${assets_index_name}", assetsIndexName);

            if (classpath != null) // Only replace if classpath is available
                argument = argument.Replace("${classpath}", effectiveClasspath);
            if (nativesDir != null) // Only replace if nativesDir is available
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
            if (rules == null || !rules.Any())
            {
                return true;
            }

            bool effectivelyAllowed = false;
            // Minecraft rule processing:
            // If no rules match, the default is allow (for arguments).
            // If a "disallow" rule matches, it's disallowed.
            // If an "allow" rule matches (and no disallow takes precedence), it's allowed.
            // The official launcher seems to default to 'allow' if no rules apply or if no rules specifically disallow the current state.
            // Let's try: default allow, unless a disallow rule matches. If an allow rule matches, it's definitely allowed (unless a disallow also matches).
            // This is often simplified to: last matching rule wins, or specific precedence.
            // For arguments, it's typically: is this argument allowed on this OS/feature set?

            bool finalAllowance = true; // Default to allowed, a disallow rule can override this
                                        // For arguments specifically, often the pattern is: if rules exist, it's disallowed by default unless an allow rule matches.
                                        // Let's stick to the common interpretation: check all rules; if a disallow matches, it's false.
                                        // If an allow matches (and no disallow has matched for the same conditions), it's true.
                                        // The list of rules often implies an OR for allow, with any disallow taking precedence.

            bool anAllowRuleMatchedAndWasSatisfied = false;

            foreach (var rule in rules)
            {
                bool conditionMet = true;

                if (rule.Os != null)
                {
                    if (!CheckOsRule(rule.Os, javaRuntimeForJvmRules)) // Pass javaRuntime for JVM rules' OS arch check
                    {
                        conditionMet = false;
                    }
                }

                if (rule.Features != null && conditionMet) // Only check features if OS part passed (or no OS rule)
                {
                    foreach (var featureRule in rule.Features)
                    {
                        bool currentFeatureState = GetCurrentFeatureState(featureRule.Key);
                        if (currentFeatureState != featureRule.Value)
                        {
                            conditionMet = false;
                            break;
                        }
                    }
                }

                if (conditionMet) // If the conditions of THIS rule are met
                {
                    if (rule.Action == RuleAction.Disallow)
                    {
                        _logger.Verbose("Rule matched and DISALLOWED argument due to: {RuleOsName}, Features: {Features}",
                            rule.Os?.Name ?? "N/A",
                            rule.Features != null ? string.Join(",", rule.Features.Select(kv => kv.Key + "=" + kv.Value)) : "N/A");
                        return false; // A single disallow rule that matches is enough to block
                    }
                    else if (rule.Action == RuleAction.Allow)
                    {
                        _logger.Verbose("Rule matched and ALLOWED argument due to: {RuleOsName}, Features: {Features}",
                            rule.Os?.Name ?? "N/A",
                            rule.Features != null ? string.Join(",", rule.Features.Select(kv => kv.Key + "=" + kv.Value)) : "N/A");
                        anAllowRuleMatchedAndWasSatisfied = true;
                        // Don't return true yet, a subsequent disallow rule might override
                    }
                }
            }

            // If we went through all rules and no disallow rule was met,
            // then the outcome depends on whether an allow rule was met.
            // If rules are present, at least one 'allow' rule must match for the argument to be used.
            return anAllowRuleMatchedAndWasSatisfied;
        }


        private bool CheckOsRule(OperatingSystemInfo osRule, JavaRuntimeInfo javaRuntimeForJvmRules)
        {
            if (osRule == null) return true; // No OS specific part in this rule, so this condition is met.

            bool nameMatch = true;
            if (!string.IsNullOrEmpty(osRule.Name))
            {
                var currentOs = OsUtils.GetCurrentOS();
                string currentOsName = "";
                switch (currentOs)
                {
                    case OperatingSystemType.Windows: currentOsName = "windows"; break;
                    case OperatingSystemType.MacOS: currentOsName = "osx"; break;
                    case OperatingSystemType.Linux: currentOsName = "linux"; break;
                    default: _logger.Warning("CheckOsRule: Unknown current OS type {CurrentOs}", currentOs); return false; // Cannot match name
                }
                nameMatch = osRule.Name.Equals(currentOsName, StringComparison.OrdinalIgnoreCase);
                if (!nameMatch) return false; // If name doesn't match, the whole OS rule fails
            }

            if (!string.IsNullOrEmpty(osRule.Version))
            {
                _logger.Verbose("OS version rule found ('{RuleVersion}') for OS '{OsName}'. Version regex matching is complex and not fully implemented here. Assuming pass for now.",
                    osRule.Version, osRule.Name ?? "Any");
                // bool versionOk = Regex.IsMatch(Environment.OSVersion.VersionString, osRule.Version);
                // if (!versionOk) return false;
            }

            if (!string.IsNullOrEmpty(osRule.Arch))
            {
                // This 'arch' is for the OS, not necessarily the JVM, unless it's a JVM argument rule.
                // The official launcher uses a specific property for JVM arch usually.
                // For simplicity, we use the current process architecture.
                ArchitectureType currentHostArch = OsUtils.GetCurrentArchitecture();
                string currentHostArchString = "";
                switch(currentHostArch)
                {
                    case ArchitectureType.X86: currentHostArchString = "x86"; break;
                    case ArchitectureType.X64: currentHostArchString = "x64"; break;
                    case ArchitectureType.Arm: currentHostArchString = "arm"; break; // Or "arm32" if rules use that
                    case ArchitectureType.Arm64: currentHostArchString = "arm64"; break; // Or "aarch64"
                    default: _logger.Warning("CheckOsRule: Unknown current host architecture {CurrentArch}", currentHostArch); return false;
                }

                // JVM argument rules sometimes target the JVM's arch, which might differ from host (e.g. 32-bit JVM on 64-bit OS)
                // If javaRuntimeForJvmRules is provided, we *could* try to infer its architecture,
                // but JavaVersionInfo doesn't store arch. So, we rely on host arch.
                // This is a common simplification unless explicit JVM arch detection is added.

                if (!osRule.Arch.Equals(currentHostArchString, StringComparison.OrdinalIgnoreCase))
                {
                     _logger.Verbose("OS arch mismatch for rule. Rule Arch: {RuleArch}, Current Host Arch: {CurrentHostArch}", osRule.Arch, currentHostArchString);
                    return false;
                }
            }
            return true; // All specified OS conditions matched
        }

        private bool GetCurrentFeatureState(string featureName)
        {
            if (featureName.Equals("is_demo_user", StringComparison.OrdinalIgnoreCase)) return _isDemoUser;
            if (featureName.Equals("has_custom_resolution", StringComparison.OrdinalIgnoreCase)) return _hasCustomResolution;
            if (featureName.Equals("has_quick_plays_support", StringComparison.OrdinalIgnoreCase)) return _hasQuickPlaysSupport;
            if (featureName.Equals("is_quick_play_singleplayer", StringComparison.OrdinalIgnoreCase)) return _isQuickPlaySingleplayer;
            if (featureName.Equals("is_quick_play_multiplayer", StringComparison.OrdinalIgnoreCase)) return _isQuickPlayMultiplayer;
            if (featureName.Equals("is_quick_play_realms", StringComparison.OrdinalIgnoreCase)) return _isQuickPlayRealms;

            _logger.Warning("Checked unknown feature in rule evaluation: {FeatureName}. Assuming 'false'.", featureName);
            return false;
        }
        private void LogPathString(string prefix, string pathString, int previewLength)
        {
            if (pathString.Length > previewLength)
            {
                _logger.Verbose("{Prefix} (first {Length} chars): {PathPreview}...", prefix, previewLength, pathString.Substring(0, previewLength));
            }
            else
            {
                 _logger.Verbose("{Prefix}: {PathString}", prefix, pathString);
            }
            // For very verbose debugging, you might log the full string at Trace level
            // _logger.ForContext("FullDetail", true).Verbose("{Prefix}: {PathString}", prefix, pathString);
        }
    }
}