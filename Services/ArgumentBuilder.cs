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

        // These would be populated by your authentication flow
        private string _authPlayerName = "${auth_player_name}"; // Default placeholder
        private string _authUuid = "${auth_uuid}";
        private string _authAccessToken = "${auth_access_token}";
        private string _clientId = "${clientid}"; // Often same as access token or a specific app ID
        private string _authXuid = "${auth_xuid}"; // Optional
        private string _userType = "msa"; // "msa" or "legacy"

        // These might come from launcher settings
        private string _launcherName = "ObsidianLauncher.NET";
        private string _launcherVersion = "0.1";
        private string _resolutionWidth = "854";
        private string _resolutionHeight = "480";
        private bool _hasCustomResolution = false; // Set to true if user provides width/height
        private bool _isDemoUser = false; // Example feature flag

        // Quick Play (example placeholders, these would come from external source/UI)
        private bool _hasQuickPlaysSupport = false;
        private string _quickPlayPath = "${quickPlayPath}";
        private string _quickPlaySingleplayer = "${quickPlaySingleplayer}";
        private string _quickPlayMultiplayer = "${quickPlayMultiplayer}";
        private string _quickPlayRealms = "${quickPlayRealms}";
        private bool _isQuickPlaySingleplayer = false;
        private bool _isQuickPlayMultiplayer = false;
        private bool _isQuickPlayRealms = false;


        public ArgumentBuilder(LauncherConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = Log.ForContext<ArgumentBuilder>();
            _logger.Verbose("ArgumentBuilder initialized.");
        }

        /// <summary>
        /// Sets authentication information to be used for replacing placeholders in arguments.
        /// </summary>
        public void SetAuthInfo(string playerName, string uuid, string accessToken, string userType = "msa", string xuid = null, string clientId = null)
        {
            _authPlayerName = playerName ?? _authPlayerName;
            _authUuid = uuid ?? _authUuid;
            _authAccessToken = accessToken ?? _authAccessToken;
            _userType = userType ?? _userType;
            _authXuid = xuid ?? _authXuid;
            _clientId = clientId ?? _authAccessToken; // Default client ID to access token if not provided
            _logger.Information("Auth info set for argument building: Player={PlayerName}, UserType={UserType}", _authPlayerName, _userType);
        }

        /// <summary>
        /// Sets custom resolution for game arguments.
        /// </summary>
        public void SetCustomResolution(int width, int height)
        {
            _resolutionWidth = width.ToString();
            _resolutionHeight = height.ToString();
            _hasCustomResolution = true;
            _logger.Information("Custom resolution set: {Width}x{Height}", width, height);
        }
        
        /// <summary>
        /// Sets a specific feature flag for rule evaluation.
        /// </summary>
        public void SetFeatureFlag(string featureName, bool value)
        {
            // This is a simplified way. A more robust system might use a dictionary.
            if (featureName.Equals("is_demo_user", StringComparison.OrdinalIgnoreCase)) _isDemoUser = value;
            else if (featureName.Equals("has_custom_resolution", StringComparison.OrdinalIgnoreCase)) _hasCustomResolution = value; // This should be set by SetCustomResolution
            else if (featureName.Equals("has_quick_plays_support", StringComparison.OrdinalIgnoreCase)) _hasQuickPlaysSupport = value;
            else if (featureName.Equals("is_quick_play_singleplayer", StringComparison.OrdinalIgnoreCase)) _isQuickPlaySingleplayer = value;
            else if (featureName.Equals("is_quick_play_multiplayer", StringComparison.OrdinalIgnoreCase)) _isQuickPlayMultiplayer = value;
            else if (featureName.Equals("is_quick_play_realms", StringComparison.OrdinalIgnoreCase)) _isQuickPlayRealms = value;
            else _logger.Warning("Attempted to set unknown feature flag for arguments: {FeatureName}", featureName);
        }


        /// <summary>
        /// Constructs the full classpath string.
        /// </summary>
        /// <param name="clientJarPath">Full path to the client JAR.</param>
        /// <param name="libraryJarPaths">List of full paths to all applicable library JARs.</param>
        /// <returns>The platform-specific classpath string.</returns>
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
            else if(!string.IsNullOrWhiteSpace(clientJarPath))
            {
                _logger.Warning("Client JAR path specified for classpath ({ClientJarPath}) but file does not exist.", clientJarPath);
            }
            else
            {
                 _logger.Warning("Client JAR path was null or empty for classpath construction.");
            }


            string classpathString = string.Join(Path.PathSeparator.ToString(), allEntries.Distinct()); // Distinct to avoid duplicates
            _logger.Information("Classpath constructed with {Count} entries.", allEntries.Distinct().Count());
            _logger.Verbose("Classpath Preview (first 500 chars): {ClasspathPreview}",
                classpathString.Length > 500 ? classpathString.Substring(0, 500) + "..." : classpathString);
            return classpathString;
        }

        /// <summary>
        /// Builds the list of JVM arguments for launching Minecraft.
        /// </summary>
        public List<string> BuildJvmArguments(
            MinecraftVersion mcVersion,
            string classpath,
            string nativesDir,
            JavaRuntimeInfo javaRuntime) // Added javaRuntime for potential arch-specific JVM args from rules
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
                        if (AreRulesSatisfied(conditionalArg.Rules, javaRuntime)) // Pass javaRuntime for OS/Arch checks
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
                _logger.Information("No modern JVM arguments structure found for {VersionId}. Default/legacy JVM arguments might be needed (not yet implemented here).", mcVersion.Id);
                // Add default JVM args for very old versions if necessary (e.g. -Xmx, -Djava.library.path)
                // This simple builder doesn't replicate the complex default argument generation of older official launchers.
                jvmArgs.Add($"-Djava.library.path={nativesDir}"); // Essential default
                jvmArgs.Add("-cp");
                jvmArgs.Add(classpath);
            }
            
            // Add client logging argument if specified
            if (mcVersion.Logging?.Client?.File != null && !string.IsNullOrEmpty(mcVersion.Logging.Client.Argument))
            {
                string logConfigFileId = mcVersion.Logging.Client.File.Id;
                string logConfigFilePath = Path.Combine(_config.AssetIndexesDir, logConfigFileId); // Or a dedicated log_configs dir
                // Ensure log config file is downloaded by AssetManager or another service
                if (File.Exists(logConfigFilePath))
                {
                    string loggingArg = mcVersion.Logging.Client.Argument.Replace("${path}", Path.GetFullPath(logConfigFilePath));
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

        /// <summary>
        /// Builds the list of game arguments for launching Minecraft.
        /// </summary>
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
                        if (AreRulesSatisfied(conditionalArg.Rules, null)) // Game arg rules usually don't depend on Java runtime specifics
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
                // Split the legacy string and replace placeholders
                var legacyArgs = mcVersion.MinecraftArguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                gameArgs.AddRange(legacyArgs.Select(arg => ReplacePlaceholders(arg, mcVersion, null, null)));
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
            // Basic direct replacements
            argument = argument.Replace("${auth_player_name}", _authPlayerName);
            argument = argument.Replace("${auth_uuid}", _authUuid);
            argument = argument.Replace("${auth_access_token}", _authAccessToken);
            argument = argument.Replace("${clientid}", _clientId);
            argument = argument.Replace("${auth_xuid}", _authXuid ?? ""); // Ensure not null
            argument = argument.Replace("${user_type}", _userType);

            argument = argument.Replace("${version_name}", mcVersion.Id);
            argument = argument.Replace("${version_type}", mcVersion.Type);

            // Paths (ensure they are properly quoted if they contain spaces for command line)
            // For simplicity here, not adding quotes, but a real launcher should.
            argument = argument.Replace("${game_directory}", Path.GetFullPath(_config.BaseDataPath)); // Or instance specific dir
            argument = argument.Replace("${assets_root}", Path.GetFullPath(_config.AssetsDir));
            argument = argument.Replace("${assets_index_name}", mcVersion.AssetIndex?.Id ?? mcVersion.Assets);

            if (classpath != null)
                argument = argument.Replace("${classpath}", classpath);
            if (nativesDir != null)
                argument = argument.Replace("${natives_directory}", Path.GetFullPath(nativesDir));

            argument = argument.Replace("${launcher_name}", _launcherName);
            argument = argument.Replace("${launcher_version}", _launcherVersion);

            // Resolution (only if feature flag has_custom_resolution is active and these are set)
            argument = argument.Replace("${resolution_width}", _resolutionWidth);
            argument = argument.Replace("${resolution_height}", _resolutionHeight);

            // Quick Play
            argument = argument.Replace("${quickPlayPath}", _quickPlayPath);
            argument = argument.Replace("${quickPlaySingleplayer}", _quickPlaySingleplayer);
            argument = argument.Replace("${quickPlayMultiplayer}", _quickPlayMultiplayer);
            argument = argument.Replace("${quickPlayRealms}", _quickPlayRealms);


            // Add more replacements as needed based on the JSON spec and your launcher features
            return argument;
        }

        private bool AreRulesSatisfied(List<ArgumentRuleCondition> rules, JavaRuntimeInfo javaRuntimeForJvmRules)
        {
            if (rules == null || !rules.Any())
            {
                return true; // No rules means the argument is unconditional (or default allow)
            }

            // Minecraft's rule logic: default to not applying if rules exist.
            // An 'allow' rule must match. If a 'disallow' rule matches, it's blocked.
            // A common interpretation: if any disallow matches -> false. Else if any allow matches -> true. Else (no matching rules) -> false.
            // Or: process in order, last matching rule wins, with a default. The spec implies a simpler "allow if conditions met, else disallow".
            // Let's go with: if a disallow rule matches, it's false. Otherwise, if an allow rule matches, it's true. Default to false if no rules match.

            bool effectivelyAllowed = false; // Default to false if rules are present but none specifically allow
            bool defaultActionIsAllow = rules.All(r => r.Action == RuleAction.Allow); // If all rules are 'allow', the implicit default might be 'disallow' if none match.
                                                                                      // If there's a mix, or only disallows, the implicit default is 'allow' unless a disallow hits.
                                                                                      // This is complex. Let's simplify: an item is allowed if an "allow" rule matches AND no "disallow" rule matches.

            foreach (var rule in rules)
            {
                bool conditionMet = true;

                if (rule.Os != null)
                {
                    conditionMet &= CheckOsRule(rule.Os, javaRuntimeForJvmRules);
                }

                if (rule.Features != null && conditionMet)
                {
                    foreach (var featureRule in rule.Features)
                    {
                        // This needs to check your launcher's current feature state
                        bool currentFeatureState = GetCurrentFeatureState(featureRule.Key);
                        if (currentFeatureState != featureRule.Value)
                        {
                            conditionMet = false;
                            break;
                        }
                    }
                }

                if (conditionMet)
                {
                    if (rule.Action == RuleAction.Allow)
                    {
                        effectivelyAllowed = true; // At least one allow condition is met
                    }
                    else if (rule.Action == RuleAction.Disallow)
                    {
                        return false; // Any matching disallow rule immediately blocks
                    }
                }
            }
            return effectivelyAllowed;
        }

        private bool CheckOsRule(OperatingSystemInfo osRule, JavaRuntimeInfo javaRuntimeForJvmRules)
        {
            if (osRule == null) return true;

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
                    default: currentOsName = "unknown"; break;
                }
                nameMatch = osRule.Name.Equals(currentOsName, StringComparison.OrdinalIgnoreCase);
            }
            if (!nameMatch) return false;

            // OS Version - Very simplified, regex matching is complex.
            // Real launchers often check specific known versions or skip this if too broad.
            if (!string.IsNullOrEmpty(osRule.Version))
            {
                 _logger.Verbose("OS version rule '{RuleVersion}' present but not fully implemented for matching. Assuming pass.", osRule.Version);
                // For a real implementation:
                // try { if (!Regex.IsMatch(Environment.OSVersion.VersionString, osRule.Version)) return false; }
                // catch (ArgumentException ex) { _logger.Warning(ex, "Invalid regex in OS version rule: {RuleVersion}", osRule.Version); return false; }
            }

            // OS Architecture - More relevant for JVM arguments typically
            if (!string.IsNullOrEmpty(osRule.Arch) && javaRuntimeForJvmRules != null) // Arch usually for JVM
            {
                // This needs to map JavaRuntimeInfo.Arch (if you add it) or derive from OsUtils.GetCurrentArchitecture()
                // to match strings like "x86", "x64" used in rules.
                string currentArch = OsUtils.GetCurrentArchitecture().ToString().ToLowerInvariant();
                if (currentArch == "arm") currentArch = "arm32"; // Align with some rule conventions
                if (currentArch == "arm64") currentArch = "aarch64"; // Or just "arm64" depending on rule usage

                if (!osRule.Arch.Equals(currentArch, StringComparison.OrdinalIgnoreCase))
                {
                    // Special case for "x86" sometimes meaning 32-bit on x64 systems if a 32-bit JVM is used
                    // This logic can get complex depending on how Mojang writes rules.
                    // For now, direct match.
                    _logger.Verbose("OS arch mismatch for rule. Rule Arch: {RuleArch}, Current Arch: {CurrentArch}", osRule.Arch, currentArch);
                    return false;
                }
            }
            return true;
        }
        
        private bool GetCurrentFeatureState(string featureName)
        {
            // This is where your launcher would check its own state for these features.
            // These are often controlled by launcher settings or user account type.
            if (featureName.Equals("is_demo_user", StringComparison.OrdinalIgnoreCase)) return _isDemoUser;
            if (featureName.Equals("has_custom_resolution", StringComparison.OrdinalIgnoreCase)) return _hasCustomResolution;
            if (featureName.Equals("has_quick_plays_support", StringComparison.OrdinalIgnoreCase)) return _hasQuickPlaysSupport;
            if (featureName.Equals("is_quick_play_singleplayer", StringComparison.OrdinalIgnoreCase)) return _isQuickPlaySingleplayer;
            if (featureName.Equals("is_quick_play_multiplayer", StringComparison.OrdinalIgnoreCase)) return _isQuickPlayMultiplayer;
            if (featureName.Equals("is_quick_play_realms", StringComparison.OrdinalIgnoreCase)) return _isQuickPlayRealms;

            _logger.Warning("Checked unknown feature: {FeatureName}. Assuming false.", featureName);
            return false;
        }
    }
}