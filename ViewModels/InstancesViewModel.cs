using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using ObsidianLauncher.Models;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class InstancesViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<InstancesViewModel>();
    
    public ObservableCollection<Instance> Instances { get; }
    public ReactiveCommand<Instance, Unit> LaunchInstanceCommand { get; }
    public ReactiveCommand<Instance, Unit> EditInstanceCommand { get; }
    public ReactiveCommand<Instance, Unit> DeleteInstanceCommand { get; }

    public InstancesViewModel()
    {
        Instances = new ObservableCollection<Instance>();
        
        LaunchInstanceCommand = ReactiveCommand.Create<Instance>(LaunchInstance);
        EditInstanceCommand = ReactiveCommand.Create<Instance>(EditInstance);
        DeleteInstanceCommand = ReactiveCommand.Create<Instance>(DeleteInstance);
        
        LoadInstances();
    }

    private void LaunchInstance(Instance instance)
    {
        _logger.Information("Launching instance: {InstanceName}", instance.Name);
        // TODO: Implement launch logic
    }

    private void EditInstance(Instance instance)
    {
        _logger.Information("Editing instance: {InstanceName}", instance.Name);
        // TODO: Open instance settings dialog
    }

    private void DeleteInstance(Instance instance)
    {
        _logger.Information("Deleting instance: {InstanceName}", instance.Name);
        // TODO: Implement delete logic with confirmation
    }

    private void LoadInstances()
    {
        // TODO: Load real instances from InstanceManager
        // For now, add some sample data for UI testing
        _logger.Information("Loading instances...");
    }
}