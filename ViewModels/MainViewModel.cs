using ReactiveUI;
using System.Reactive;
using HyPrism.Backend;
using System.Threading.Tasks;
using System;
using Avalonia.Threading;
using System.Collections.ObjectModel;

namespace HyPrism.ViewModels;

public class MainViewModel : ReactiveObject
{
    public AppService AppService { get; }

    // User Profile
    private string _nick;
    public string Nick
    {
        get => _nick;
        set => this.RaiseAndSetIfChanged(ref _nick, value);
    }
    
    // Status
    private bool _isGameRunning;
    public bool IsGameRunning
    {
        get => _isGameRunning;
        set => this.RaiseAndSetIfChanged(ref _isGameRunning, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
    }

    private double _progressValue; // 0-100
    public double ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    private string _progressText = "Ready";
    public string ProgressText
    {
        get => _progressText;
        set => this.RaiseAndSetIfChanged(ref _progressText, value);
    }

    private string _downloadSpeedText = "";
    public string DownloadSpeedText
    {
        get => _downloadSpeedText;
        set => this.RaiseAndSetIfChanged(ref _downloadSpeedText, value);
    }
    
    // Versioning
    private string _selectedBranch = "Release";
    public string SelectedBranch
    {
        get => _selectedBranch;
        set => this.RaiseAndSetIfChanged(ref _selectedBranch, value);
    }
    
    private int _selectedVersion = 0;
    public int SelectedVersion
    {
        get => _selectedVersion;
        set => this.RaiseAndSetIfChanged(ref _selectedVersion, value);
    }

    // Overlays
    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    private bool _isConfigOpen; // For ModManager or others
    public bool IsModsOpen
    {
        get => _isConfigOpen;
        set => this.RaiseAndSetIfChanged(ref _isConfigOpen, value);
    }

    private readonly ObservableAsPropertyHelper<bool> _isOverlayOpen;
    public bool IsOverlayOpen => _isOverlayOpen.Value;
    
    // Child ViewModels for Overlays
    private SettingsViewModel? _settingsViewModel;
    public SettingsViewModel? SettingsViewModel
    {
        get => _settingsViewModel;
        set => this.RaiseAndSetIfChanged(ref _settingsViewModel, value);
    }

    private ModManagerViewModel? _modManagerViewModel;
    public ModManagerViewModel? ModManagerViewModel
    {
        get => _modManagerViewModel;
        set => this.RaiseAndSetIfChanged(ref _modManagerViewModel, value);
    }

    public ObservableCollection<string> Branches { get; } = new() { "Release", "Pre-Release" };

    // Commands
    public ReactiveCommand<Unit, Unit> LaunchCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleModsCommand { get; }
    
    public MainViewModel()
    {
        AppService = new AppService();
        _nick = AppService.Configuration.Nick;
        
        // Output properties
        _isOverlayOpen = this.WhenAnyValue(x => x.IsSettingsOpen, x => x.IsModsOpen, (s, m) => s || m)
                             .ToProperty(this, x => x.IsOverlayOpen);
        
        // Initialize child VMs
        SettingsViewModel = new SettingsViewModel(AppService);
        SettingsViewModel.CloseCommand.Subscribe(_ => IsSettingsOpen = false);

        // Commands
        LaunchCommand = ReactiveCommand.CreateFromTask(LaunchAsync);
        OpenFolderCommand = ReactiveCommand.Create(() => 
        {
            var branch = SelectedBranch?.ToLower().Replace(" ", "-") ?? "release";
            AppService.OpenInstanceFolder(branch, SelectedVersion);
        });
        
        ToggleSettingsCommand = ReactiveCommand.Create(() => 
        {
            IsSettingsOpen = !IsSettingsOpen;
            if (IsSettingsOpen) IsModsOpen = false;
        });
        
        ToggleModsCommand = ReactiveCommand.Create(() => 
        {
            if (!IsModsOpen)
            {
                var branch = SelectedBranch?.ToLower().Replace(" ", "-") ?? "release"; 
                ModManagerViewModel = new ModManagerViewModel(AppService, branch, SelectedVersion);
                ModManagerViewModel.CloseCommand.Subscribe(_ => IsModsOpen = false);
            }
            IsModsOpen = !IsModsOpen;
            if (IsModsOpen) IsSettingsOpen = false;
        });
        
        // Event subscriptions need to be marshaled to UI thread
        AppService.DownloadProgressChanged += (state, progress, speed, dl, total) => 
            Dispatcher.UIThread.Post(() => OnDownloadProgress(state, progress, speed, dl, total));
            
        AppService.GameStateChanged += (state, code) => 
            Dispatcher.UIThread.Post(() => OnGameStateChanged(state, code));
            
        AppService.ErrorOccurred += (title, msg, trace) => 
            Dispatcher.UIThread.Post(() => OnError(title, msg, trace));
    }

    private void OnDownloadProgress(string state, double progress, string speed, long downloaded, long total)
    {
        IsDownloading = true;
        ProgressValue = progress * 100;
        ProgressText = state;
        DownloadSpeedText = speed;
    }

    private void OnGameStateChanged(string state, int code)
    {
        // Adjust logic based on actual AppService notification codes
        // Assuming code > 0 is running state or similar
        ProgressText = state;
        IsGameRunning = state.ToLower().Contains("running") || state.ToLower().Contains("playing");
        
        if (!IsGameRunning && !IsDownloading)
        {
             ProgressValue = 0;
             DownloadSpeedText = "";
        }
    }

    private void OnError(string title, string message, string? trace)
    {
         ProgressText = $"Error: {message}";
         IsDownloading = false;
    }

    private async Task LaunchAsync()
    {
        if (IsGameRunning) return;
        
        try 
        {
            // Reset state
            IsDownloading = true;
            ProgressText = "Preparing...";
            
            await AppService.DownloadAndLaunchAsync();
        }
        catch (Exception ex)
        {
            OnError("Launch Error", ex.Message, ex.StackTrace);
        }
        finally
        {
             // If game started, logic is handled by event. 
        }
    }
}
