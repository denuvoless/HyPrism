using ReactiveUI;
using System.Reactive;
using HyPrism.Backend;
using System.Collections.Generic;

namespace HyPrism.ViewModels;

public class SettingsViewModel : ReactiveObject
{
    private readonly AppService _appService;

    // Tabs
    private string _activeTab = "profile";
    public string ActiveTab
    {
        get => _activeTab;
        set => this.RaiseAndSetIfChanged(ref _activeTab, value);
    }

    // Profile
    public string Nick
    {
        get => _appService.Configuration.Nick;
        set
        {
            if (_appService.Configuration.Nick != value)
            {
                _appService.Configuration.Nick = value;
                _appService.SaveConfig();
                this.RaisePropertyChanged();
            }
        }
    }

    // General
    public bool CloseAfterLaunch
    {
        get => _appService.Configuration.CloseAfterLaunch;
        set
        {
            if (_appService.Configuration.CloseAfterLaunch != value)
            {
                _appService.Configuration.CloseAfterLaunch = value;
                _appService.SaveConfig();
                this.RaisePropertyChanged();
            }
        }
    }

    public bool DisableNews
    {
        get => _appService.Configuration.DisableNews;
        set
        {
            if (_appService.Configuration.DisableNews != value)
            {
                _appService.Configuration.DisableNews = value;
                _appService.SaveConfig();
                this.RaisePropertyChanged();
            }
        }
    }
    
    // Commands
    public ReactiveCommand<string, Unit> SwitchTabCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; } // Handled by View

    public SettingsViewModel(AppService appService)
    {
        _appService = appService;
        SwitchTabCommand = ReactiveCommand.Create<string>(tab => ActiveTab = tab);
        CloseCommand = ReactiveCommand.Create(() => { });
    }
}
