using System;
using System.Runtime.InteropServices;
using Avalonia;
using HyPrism.Backend;

using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;

namespace HyPrism;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
            
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .With(new SkiaOptions { UseOpacitySaveLayer = true })
            .LogToTrace();
            
}
