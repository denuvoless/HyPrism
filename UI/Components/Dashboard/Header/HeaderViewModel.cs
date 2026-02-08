using System;
using System.IO;
using System.Reactive;
using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using HyPrism.Services;
using HyPrism.Services.Core;
using HyPrism.Services.Game;

namespace HyPrism.UI.Components.Dashboard.Header;

public class HeaderViewModel : ReactiveObject
{
    private readonly ConfigService _configService;
    private readonly AvatarService _avatarService;
    private readonly string _appDir;
    
    // Commands exposed to UI
    public ReactiveCommand<Unit, Unit> ToggleProfileEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSettingsCommand { get; }
    
    // Properties
    private string _nick;
    public string Nick
    {
        get => _nick;
        set => this.RaiseAndSetIfChanged(ref _nick, value);
    }
    
    // Avatar image loaded from game cache
    private Bitmap? _avatarImage;
    public Bitmap? AvatarImage
    {
        get => _avatarImage;
        set => this.RaiseAndSetIfChanged(ref _avatarImage, value);
    }
    
    public bool HasAvatar => AvatarImage != null;
    
    // Version string read from assembly (set in .csproj <Version>)
    public string AppVersion { get; }

    public HeaderViewModel(
        ConfigService configService,
        Action toggleProfileEditor, 
        Action toggleSettings,
        LocalizationService localizationService,
        AvatarService avatarService,
        string appDir)
    {
        _configService = configService;
        _avatarService = avatarService;
        _appDir = appDir;
        _nick = _configService.Configuration.Nick;
        
        // Read version from assembly metadata (populated from <Version> in .csproj)
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";
        // Strip any +commitHash suffix
        var plusIndex = version.IndexOf('+');
        if (plusIndex > 0) version = version[..plusIndex];
        AppVersion = $"HyPrism {version}";
        
        // Commands
        ToggleProfileEditorCommand = ReactiveCommand.Create(toggleProfileEditor);
        ToggleSettingsCommand = ReactiveCommand.Create(toggleSettings);
        
        // Load existing avatar on startup
        LoadAvatar();
        
        // Listen for avatar updates from game exit
        _avatarService.AvatarUpdated += OnAvatarUpdated;
        
        // Re-check HasAvatar when AvatarImage changes
        this.WhenAnyValue(x => x.AvatarImage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasAvatar)));
    }

    public void RefreshNick()
    {
        Nick = _configService.Configuration.Nick;
        // Also refresh avatar in case profile changed
        LoadAvatar();
    }
    
    private void LoadAvatar()
    {
        try
        {
            var uuid = _configService.Configuration.UUID;
            if (string.IsNullOrWhiteSpace(uuid)) return;
            
            var avatarPath = Path.Combine(_appDir, "AvatarBackups", $"{uuid}.png");
            if (File.Exists(avatarPath))
            {
                using var stream = File.OpenRead(avatarPath);
                AvatarImage = new Bitmap(stream);
            }
        }
        catch (Exception)
        {
            // Silently fail — fallback to user icon
        }
    }
    
    private void OnAvatarUpdated(string avatarPath)
    {
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    using var stream = File.OpenRead(avatarPath);
                    AvatarImage = new Bitmap(stream);
                }
                catch { }
            });
        }
        catch { }
    }
}
