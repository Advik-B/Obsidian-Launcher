using ReactiveUI;

namespace ObsidianLauncher.ViewModels;

public class MinecraftInstanceViewModel : ViewModelBase
{
    private string _name;
    private string _version;
    private string _type;
    private string _description;

    public MinecraftInstanceViewModel(string name, string version, string type = "Release", string description = "")
    {
        _name = name;
        _version = version;
        _type = type;
        _description = description;
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Version
    {
        get => _version;
        set => this.RaiseAndSetIfChanged(ref _version, value);
    }

    public string Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public string DisplayText => $"{Name} ({Version})";
}