using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HyPrism.ViewModels;
using System;

namespace HyPrism;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }
    
    // Constructor injection support would be nice, but for now we set DataContext manually after init
    // or use a static Locator. 
    // Here we assume DataContext is set by the caller (MainViewModel).
}
