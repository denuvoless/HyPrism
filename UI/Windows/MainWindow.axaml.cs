using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyPrism.UI.ViewModels;

namespace HyPrism.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        
        try
        {
            var assets = AssetLoader.Open(new Uri("avares://HyPrism/Assets/logo.png"));
            Icon = new WindowIcon(assets);
        }
        catch
        {
            // Fallback for default icon if loading fails
        }
    }
}
