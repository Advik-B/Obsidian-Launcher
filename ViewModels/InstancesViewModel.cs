using System;
using System.Collections.ObjectModel;
using System.Reactive;
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
    
    public ObservableCollection<Instance> Instances { get; }
    public ReactiveCommand<Instance, Unit> LaunchInstanceCommand { get; }
    public ReactiveCommand<Instance, Unit> EditInstanceCommand { get; }
    public ReactiveCommand<Instance, Unit> DeleteInstanceCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public InstancesViewModel(InstanceManager instanceManager, Action<Instance>? showInstanceSettings = null)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _showInstanceSettings = showInstanceSettings;
        Instances = new ObservableCollection<Instance>();
        
        LaunchInstanceCommand = ReactiveCommand.Create<Instance>(LaunchInstance);
        EditInstanceCommand = ReactiveCommand.Create<Instance>(EditInstance);
        DeleteInstanceCommand = ReactiveCommand.Create<Instance>(DeleteInstance);
        RefreshCommand = ReactiveCommand.Create(LoadInstances);
        
        LoadInstances();
    }

    private void LaunchInstance(Instance instance)
    {
        _logger.Information("Launching instance: {InstanceName}", instance.Name);
        // TODO: Implement launch logic by integrating with existing game launcher
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

    private void DeleteInstance(Instance instance)
    {
        _logger.Information("Deleting instance: {InstanceName}", instance.Name);
        // TODO: Implement delete logic with confirmation
        Instances.Remove(instance);
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
}