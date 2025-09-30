using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class InstancesViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<InstancesViewModel>();
    private readonly InstanceManager _instanceManager;
    private readonly Action<Instance>? _showInstanceSettings;
    private readonly Action? _createInstance;
    private readonly Dictionary<string, CancellationTokenSource> _setupCancellationTokens = new();
    
    public ObservableCollection<Instance> Instances { get; }
    public ReactiveCommand<Instance, Unit> LaunchInstanceCommand { get; }
    public ReactiveCommand<Instance, Unit> EditInstanceCommand { get; }
    public ReactiveCommand<Instance, Unit> DeleteInstanceCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateInstanceCommand { get; }

    public InstancesViewModel(InstanceManager instanceManager, Action<Instance>? showInstanceSettings = null, Action? createInstance = null)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _showInstanceSettings = showInstanceSettings;
        _createInstance = createInstance;
        Instances = new ObservableCollection<Instance>();
        
        LaunchInstanceCommand = ReactiveCommand.Create<Instance>(LaunchInstance);
        EditInstanceCommand = ReactiveCommand.Create<Instance>(EditInstance);
        DeleteInstanceCommand = ReactiveCommand.Create<Instance>(DeleteInstance);
        RefreshCommand = ReactiveCommand.Create(LoadInstances);
        CreateInstanceCommand = ReactiveCommand.Create(CreateInstance);
        
        LoadInstances();
    }

    private async void LaunchInstance(Instance instance)
    {
        try
        {
            // Don't allow launching if instance is not ready
            if (!instance.IsEnabled)
            {
                _logger.Information("Cannot launch instance {InstanceName} - setup not complete", instance.Name);
                return;
            }

            _logger.Information("Launching instance: {InstanceName}", instance.Name);
            
            // For now, just show a message that launching is not fully implemented
            // In a real implementation, this would:
            // 1. Get Java runtime
            // 2. Build JVM arguments
            // 3. Build classpath from libraries
            // 4. Build game arguments
            // 5. Launch using GameLauncher
            
            _logger.Information("Instance launch requested for {InstanceName} - Implementation pending", instance.Name);
            
            // Update last played date to show interaction
            instance.LastPlayedDate = DateTime.UtcNow;
            if (_instanceManager != null)
            {
                await _instanceManager.SaveInstanceAsync(instance);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch instance: {InstanceName}", instance.Name);
        }
    }

    private void EditInstance(Instance instance)
    {
        _logger.Information("Editing instance: {InstanceName}", instance.Name);
        
        if (_showInstanceSettings != null)
        {
            _showInstanceSettings(instance);
        }
        else
        {
            // Fallback: Create settings view model but no way to show it
            _logger.Warning("No callback provided to show instance settings");
        }
    }

    private async void DeleteInstance(Instance instance)
    {
        _logger.Information("Deleting instance: {InstanceName}", instance.Name);
        // TODO: Implement delete logic with confirmation
        Instances.Remove(instance);
    }

    private void CreateInstance()
    {
        _logger.Information("Create instance requested from instances view");
        _createInstance?.Invoke();
    }

    private async void LoadInstances()
    {
        try
        {
            _logger.Information("Loading instances...");
            var instances = await _instanceManager.GetAllInstancesAsync();
            
            Instances.Clear();
            foreach (var instance in instances)
            {
                Instances.Add(instance);
            }
            
            _logger.Information("Loaded {Count} instances", instances.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load instances: {Message}", ex.Message);
        }
    }

    public async void StartInstanceSetup(Instance instance)
    {
        try
        {
            _logger.Information("Starting background setup for instance: {InstanceName}", instance.Name);

            // Cancel any existing setup for this instance
            if (_setupCancellationTokens.TryGetValue(instance.Id, out var existingCts))
            {
                existingCts.Cancel();
                _setupCancellationTokens.Remove(instance.Id);
            }

            // Create new cancellation token for this setup
            var cts = new CancellationTokenSource();
            _setupCancellationTokens[instance.Id] = cts;

            // Create a simple MinecraftVersion object for now
            // TODO: This should be obtained from a proper VersionManager service
            var mcVersion = new MinecraftVersion
            {
                Id = instance.MinecraftVersionId,
                Type = "release" // Default assumption
            };

            // Create progress handler
            var progressHandler = new Progress<InstanceSetupProgress>(progress =>
            {
                // Update the instance in our collection
                var instanceInCollection = Instances.FirstOrDefault(i => i.Id == instance.Id);
                if (instanceInCollection != null)
                {
                    instanceInCollection.SetupProgress = progress.OverallProgress;
                    instanceInCollection.SetupStatus = progress.CurrentTask;
                    instanceInCollection.IsSetupComplete = progress.IsComplete;
                }
            });

            // Start setup in background
            await Task.Run(async () =>
            {
                await _instanceManager.SetupInstanceAsync(instance, mcVersion, progressHandler, cts.Token);
            });

            // Remove from tracking when done
            _setupCancellationTokens.Remove(instance.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start instance setup: {InstanceName}", instance.Name);
        }
    }
}