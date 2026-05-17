using System;
using System.Windows.Input;
using ObsidianLauncher.Settings;

namespace ObsidianLauncher.ViewModels;

public class SetupWizardViewModel : ViewModelBase
{
    private readonly LauncherSettings _settings;
    private int _currentPage;
    private string _javaPath = "";
    private int _minMemoryMB = 512;
    private int _maxMemoryMB = 4096;

    public SetupWizardViewModel(LauncherSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _javaPath = settings.JavaPath.Value;
        _minMemoryMB = settings.MinMemoryMB.Value;
        _maxMemoryMB = settings.MaxMemoryMB.Value;

        BackCommand = new RelayCommand(() => { if (_currentPage > 0) CurrentPage--; }, () => _currentPage > 0);
        NextCommand = new RelayCommand(() => { if (_currentPage < 3) CurrentPage++; }, () => _currentPage < 3);
        FinishCommand = new RelayCommand(Finish);
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsWelcomePage));
                OnPropertyChanged(nameof(IsJavaPage));
                OnPropertyChanged(nameof(IsMemoryPage));
                OnPropertyChanged(nameof(IsFinishPage));
                OnPropertyChanged(nameof(ShowBack));
                OnPropertyChanged(nameof(ShowNext));
                OnPropertyChanged(nameof(ShowFinish));
                ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsWelcomePage => _currentPage == 0;
    public bool IsJavaPage    => _currentPage == 1;
    public bool IsMemoryPage  => _currentPage == 2;
    public bool IsFinishPage  => _currentPage == 3;
    public bool ShowBack      => _currentPage > 0;
    public bool ShowNext      => _currentPage < 3;
    public bool ShowFinish    => _currentPage == 3;

    public string JavaPath
    {
        get => _javaPath;
        set => SetProperty(ref _javaPath, value);
    }

    public int MinMemoryMB
    {
        get => _minMemoryMB;
        set => SetProperty(ref _minMemoryMB, value);
    }

    public int MaxMemoryMB
    {
        get => _maxMemoryMB;
        set => SetProperty(ref _maxMemoryMB, value);
    }

    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand FinishCommand { get; }

    public event EventHandler? WizardCompleted;

    private void Finish()
    {
        _settings.JavaPath.Value = JavaPath;
        _settings.MinMemoryMB.Value = MinMemoryMB;
        _settings.MaxMemoryMB.Value = MaxMemoryMB;
        _settings.FirstRunCompleted.Value = true;
        _settings.Save();
        WizardCompleted?.Invoke(this, EventArgs.Empty);
    }
}
