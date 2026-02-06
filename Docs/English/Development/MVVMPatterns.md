# MVVM Patterns in HyPrism

> Guide to Model-View-ViewModel patterns in HyPrism with ReactiveUI and CommunityToolkit.Mvvm.

---

## Table of Contents

- [Overview](#-overview)
- [Architecture Components](#-architecture-components)
- [ReactiveUI](#-reactiveui)
- [CommunityToolkit.Mvvm](#-communitytoolkitmvvm)
- [Dependency Injection](#-dependency-injection)
- [Data Binding](#-data-binding)
- [Patterns in HyPrism](#-patterns-in-hyprism)
- [Anti-Patterns](#-anti-patterns)
- [Codebase Examples](#-codebase-examples)

---

## 📋 Overview

HyPrism uses **MVVM** (Model-View-ViewModel) architecture:

```
┌─────────────────────────────────────────────────────────────┐
│                          View                               │
│                    (XAML markup)                            │
│         Button Command="{Binding PlayCommand}"              │
└────────────────────────┬────────────────────────────────────┘
                         │ Data Binding
                         ↓
┌─────────────────────────────────────────────────────────────┐
│                       ViewModel                             │
│                   (ReactiveObject)                          │
│    [ObservableProperty] string _status;                     │
│    [RelayCommand] void Play() { }                           │
└────────────────────────┬────────────────────────────────────┘
                         │ Dependency Injection
                         ↓
┌─────────────────────────────────────────────────────────────┐
│                        Services                             │
│         (GameSessionService, ConfigService, etc.)           │
└────────────────────────┬────────────────────────────────────┘
                         │ CRUD Operations
                         ↓
┌─────────────────────────────────────────────────────────────┐
│                         Model                               │
│            (Config, Profile, ModInfo, etc.)                 │
└─────────────────────────────────────────────────────────────┘
```

---

## 🧩 Architecture Components

### Model

**Location:** `Models/`

Models are simple data classes without business logic:

```csharp
// Models/Profile.cs
public class Profile
{
    public string UUID { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? SkinPath { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### View

**Location:** `UI/Views/`, `UI/Components/`

XAML markup with declarative bindings:

```xml
<UserControl x:DataType="vm:DashboardViewModel">
    <TextBlock Text="{Binding WelcomeMessage}"/>
    <Button Content="Play" Command="{Binding PlayCommand}"/>
</UserControl>
```

### ViewModel

**Location:** `UI/Views/*/`, `UI/MainWindow/`, `UI/Components/*/`

Presentation logic with reactive properties:

```csharp
public partial class DashboardViewModel : ReactiveObject
{
    private readonly GameSessionService _gameService;
    
    [ObservableProperty]
    private string _welcomeMessage = "Welcome!";
    
    [RelayCommand]
    private async Task PlayAsync()
    {
        await _gameService.LaunchAsync();
    }
}
```

---

## ⚛️ ReactiveUI

### ReactiveObject

Base class for all ViewModels:

```csharp
public class MyViewModel : ReactiveObject
{
    // Properties and logic
}
```

### Manual Property Declaration

```csharp
private string _status = string.Empty;
public string Status
{
    get => _status;
    set => this.RaiseAndSetIfChanged(ref _status, value);
}
```

### WhenAnyValue

Reactive observation of property changes:

```csharp
public MyViewModel()
{
    // React to single property change
    this.WhenAnyValue(x => x.Username)
        .Subscribe(username => 
        {
            IsValid = !string.IsNullOrEmpty(username);
        });
    
    // Combine multiple properties
    this.WhenAnyValue(
        x => x.Username, 
        x => x.Password,
        (user, pass) => !string.IsNullOrEmpty(user) && pass.Length >= 8)
        .ToPropertyEx(this, x => x.CanLogin);
}
```

### ObservableAsPropertyHelper (OAPH)

Computed properties based on others:

```csharp
private readonly ObservableAsPropertyHelper<bool> _canLogin;
public bool CanLogin => _canLogin.Value;

public MyViewModel()
{
    _canLogin = this.WhenAnyValue(
        x => x.Username,
        x => x.Password,
        (u, p) => !string.IsNullOrEmpty(u) && p.Length >= 8)
        .ToProperty(this, x => x.CanLogin);
}
```

### ReactiveCommand

Commands with reactive support:

```csharp
// Synchronous command
public ReactiveCommand<Unit, Unit> DoSomething { get; }

// Asynchronous command
public ReactiveCommand<Unit, Unit> LoadData { get; }

// With parameter
public ReactiveCommand<string, Unit> Search { get; }

// With execution condition
public ReactiveCommand<Unit, Unit> Submit { get; }

public MyViewModel()
{
    DoSomething = ReactiveCommand.Create(() => 
    {
        // Synchronous logic
    });
    
    LoadData = ReactiveCommand.CreateFromTask(async () =>
    {
        await _service.LoadAsync();
    });
    
    Search = ReactiveCommand.CreateFromTask<string>(async query =>
    {
        Results = await _service.SearchAsync(query);
    });
    
    // Command active only when CanSubmit = true
    var canSubmit = this.WhenAnyValue(x => x.CanSubmit);
    Submit = ReactiveCommand.Create(() => { }, canSubmit);
}
```

---

## 🧰 CommunityToolkit.Mvvm

### Source Generators

Boilerplate code generation through attributes:

### [ObservableProperty]

```csharp
// Instead of this:
private string _name;
public string Name
{
    get => _name;
    set => this.RaiseAndSetIfChanged(ref _name, value);
}

// Write:
[ObservableProperty]
private string _name;
```

⚠️ **Important:** For ReactiveUI compatibility, use `partial class` inheriting from `ReactiveObject`.

### [RelayCommand]

```csharp
// Instead of this:
public ICommand SaveCommand { get; }
public MyViewModel()
{
    SaveCommand = new RelayCommand(Save);
}
private void Save() { }

// Write:
[RelayCommand]
private void Save()
{
    _configService.Save();
}

// Asynchronous command
[RelayCommand]
private async Task LoadAsync()
{
    Data = await _service.LoadAsync();
}

// With CanExecute condition
[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { }

private bool CanSave => !string.IsNullOrEmpty(Name);
```

### [NotifyPropertyChangedFor]

Notify dependent properties:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FullName))]
private string _firstName;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FullName))]
private string _lastName;

public string FullName => $"{FirstName} {LastName}";
```

---

## 💉 Dependency Injection

### Registration in Bootstrapper

```csharp
public static class Bootstrapper
{
    public static IServiceProvider Initialize()
    {
        var services = new ServiceCollection();
        
        // Services - Singleton (one instance)
        services.AddSingleton<ConfigService>();
        services.AddSingleton<GameSessionService>();
        
        // ViewModels - Singleton for main ones
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        
        // ViewModels - Transient for modals
        services.AddTransient<SettingsViewModel>();
        
        return services.BuildServiceProvider();
    }
}
```

### Dependency Injection

```csharp
public partial class DashboardViewModel : ReactiveObject
{
    private readonly GameSessionService _gameService;
    private readonly ConfigService _configService;
    private readonly LocalizationService _localization;
    
    public DashboardViewModel(
        GameSessionService gameService,
        ConfigService configService,
        LocalizationService localization)
    {
        _gameService = gameService;
        _configService = configService;
        _localization = localization;
        
        // Initialization
    }
}
```

### Manual Service Resolution

```csharp
// In App.axaml.cs provider is available
var vm = App.Current.Services!.GetRequiredService<MyViewModel>();

// Or via GetService (returns null if not found)
var optional = App.Current.Services!.GetService<OptionalService>();
```

---

## 🔗 Data Binding

### One-Way Binding

```xml
<!-- Source → Target (ViewModel → View) -->
<TextBlock Text="{Binding UserName}"/>
```

### Two-Way Binding

```xml
<!-- Source ↔ Target -->
<TextBox Text="{Binding UserName, Mode=TwoWay}"/>
```

### Command Binding

```xml
<Button Command="{Binding SaveCommand}"
        CommandParameter="{Binding SelectedItem}"/>
```

### Element Binding

```xml
<Slider x:Name="VolumeSlider" Maximum="100"/>
<TextBlock Text="{Binding #VolumeSlider.Value}"/>
```

### Converters

```xml
<Image IsVisible="{Binding HasImage, 
                          Converter={StaticResource BoolToVisibility}}"/>
```

---

## 🧪 Patterns in HyPrism

### MainViewModel + Child ViewModels

```csharp
public partial class MainViewModel : ReactiveObject
{
    // Child ViewModels
    public LoadingViewModel LoadingVm { get; }
    public DashboardViewModel DashboardVm { get; }
    
    [ObservableProperty]
    private bool _isLoading = true;
    
    public MainViewModel(
        LoadingViewModel loadingVm,
        DashboardViewModel dashboardVm)
    {
        LoadingVm = loadingVm;
        DashboardVm = dashboardVm;
    }
    
    public async Task InitializeAsync()
    {
        await LoadingVm.LoadAsync();
        IsLoading = false;
    }
}
```

### View with Dynamic Content

```xml
<Grid>
    <!-- Loading View -->
    <views:LoadingView DataContext="{Binding LoadingVm}"
                       IsVisible="{Binding IsLoading}"/>
    
    <!-- Main Dashboard -->
    <views:DashboardView DataContext="{Binding DashboardVm}"
                         IsVisible="{Binding !IsLoading}"
                         Opacity="{Binding MainContentOpacity}"/>
</Grid>
```

### Overlay Pattern

```csharp
public partial class DashboardViewModel : ReactiveObject
{
    [ObservableProperty]
    private bool _isSettingsOpen;
    
    [ObservableProperty]
    private bool _isProfileEditorOpen;
    
    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;
    
    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;
}
```

```xml
<Grid>
    <!-- Main Content -->
    <ContentControl Content="{Binding MainContent}"/>
    
    <!-- Settings Overlay -->
    <Panel IsVisible="{Binding IsSettingsOpen}"
           Background="#80000000">
        <views:SettingsView/>
    </Panel>
</Grid>
```

---

## ⚠️ Anti-Patterns

### ❌ UI References in ViewModel

```csharp
// WRONG!
public class MyViewModel
{
    public Button PlayButton { get; set; }
    public Window MainWindow { get; set; }
}
```

### ✅ Only Data and Commands

```csharp
// CORRECT
public class MyViewModel : ReactiveObject
{
    [ObservableProperty]
    private string _buttonText;
    
    [RelayCommand]
    private void Play() { }
}
```

### ❌ Business Logic in Code-Behind

```csharp
// WRONG!
private async void Button_Click(object sender, RoutedEventArgs e)
{
    var data = await LoadFromDatabase();
    ProcessData(data);
}
```

### ✅ Logic in ViewModel or Services

```csharp
// CORRECT
[RelayCommand]
private async Task LoadDataAsync()
{
    var data = await _dataService.LoadAsync();
    _processor.Process(data);
}
```

### ❌ Creating Services Directly

```csharp
// WRONG!
public MyViewModel()
{
    _service = new MyService();
}
```

### ✅ Dependency Injection

```csharp
// CORRECT
public MyViewModel(MyService service)
{
    _service = service;
}
```

---

## 📚 Codebase Examples

### DashboardViewModel

**File:** `UI/Views/DashboardView/DashboardViewModel.cs`

Demonstrates:
- Injection of multiple services
- Reactive properties
- Commands for UI actions
- Overlay management

### MainViewModel

**File:** `UI/MainWindow/MainViewModel.cs`

Demonstrates:
- Ownership of child ViewModels
- Asynchronous initialization
- Loading state management

### SettingsViewModel

**File:** `UI/Views/SettingsView/SettingsViewModel.cs`

Demonstrates:
- Two-way binding of settings
- Validation
- Saving through services

---

## 📚 Additional Resources

- [ReactiveUI Documentation](https://www.reactiveui.net/)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [Avalonia Data Binding](https://docs.avaloniaui.net/docs/data-binding/)
- [UIComponentGuide.md](UIComponentGuide.md) — Creating Components
