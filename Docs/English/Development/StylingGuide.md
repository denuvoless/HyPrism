# Styling Guide

> Complete guide on themes, color palette, styles, and visual design in HyPrism.

---

## Table of Contents

- [Theme System](#-theme-system)
- [Color Palette](#-color-palette)
- [Typography](#-typography)
- [Button Styles](#-button-styles)
- [Card Styles](#-card-styles)
- [Input Styles](#-input-styles)
- [Borders and Spacing](#-borders-and-spacing)
- [Animations](#-animations)
- [Icons](#-icons)
- [Responsive Design](#-responsive-design)
- [Styling Checklist](#-styling-checklist)

---

## 🎨 Theme System

HyPrism uses a unified dark theme with customizable accent color.

### ThemeService

**File:** `Services/Core/ThemeService.cs`

```csharp
public class ThemeService
{
    public static ThemeService Instance { get; }
    
    public void Initialize(string hexColor);
    public void ApplyAccentColor(string hexColor);
}
```

**Initialization (App.axaml.cs):**
```csharp
ThemeService.Instance.Initialize(config.AccentColor ?? "#FFA845");
```

**Changing accent color:**
```csharp
ThemeService.Instance.ApplyAccentColor("#FF5500");
```

---

## 🎨 Color Palette

### Base Colors

Defined in `UI/Styles/SharedColors.axaml`:

```xml
<ResourceDictionary>
    <!-- Background colors -->
    <Color x:Key="BackgroundDark">#121212</Color>
    <Color x:Key="BackgroundMedium">#1E1E1E</Color>
    <Color x:Key="BackgroundLight">#2A2A2A</Color>
    <Color x:Key="BackgroundHover">#333333</Color>
    
    <!-- Text colors -->
    <Color x:Key="TextPrimary">#FFFFFF</Color>
    <Color x:Key="TextSecondary">#B0B0B0</Color>
    <Color x:Key="TextDisabled">#666666</Color>
    
    <!-- Accent color (dynamic) -->
    <Color x:Key="SystemAccentColor">#FFA845</Color>
    
    <!-- Status colors -->
    <Color x:Key="SuccessColor">#4CAF50</Color>
    <Color x:Key="WarningColor">#FF9800</Color>
    <Color x:Key="ErrorColor">#F44336</Color>
    <Color x:Key="InfoColor">#2196F3</Color>
</ResourceDictionary>
```

### Brushes

```xml
<ResourceDictionary>
    <SolidColorBrush x:Key="BackgroundDarkBrush" Color="{StaticResource BackgroundDark}"/>
    <SolidColorBrush x:Key="BackgroundMediumBrush" Color="{StaticResource BackgroundMedium}"/>
    <SolidColorBrush x:Key="BackgroundLightBrush" Color="{StaticResource BackgroundLight}"/>
    
    <SolidColorBrush x:Key="PrimaryTextBrush" Color="{StaticResource TextPrimary}"/>
    <SolidColorBrush x:Key="SecondaryTextBrush" Color="{StaticResource TextSecondary}"/>
    
    <SolidColorBrush x:Key="SystemAccentBrush" Color="{DynamicResource SystemAccentColor}"/>
</ResourceDictionary>
```

### Using Colors

```xml
<!-- Via StaticResource (static colors) -->
<Border Background="{StaticResource BackgroundMediumBrush}"/>
<TextBlock Foreground="{StaticResource PrimaryTextBrush}"/>

<!-- Via DynamicResource (dynamic colors, e.g., accent) -->
<Button Background="{DynamicResource SystemAccentBrush}"/>
```

---

## 📝 Typography

### Fonts

HyPrism uses system fonts with fallback:

```xml
<Application.Resources>
    <FontFamily x:Key="DefaultFontFamily">
        Segoe UI, SF Pro Display, -apple-system, 
        Noto Sans, sans-serif
    </FontFamily>
</Application.Resources>
```

### Text Sizes

| Purpose | Size | Weight |
|---------|------|--------|
| H1 (Title) | 24px | SemiBold |
| H2 (Subtitle) | 18px | SemiBold |
| H3 (Section) | 16px | Medium |
| Body (Text) | 14px | Regular |
| Caption | 12px | Regular |
| Micro (Small) | 10px | Regular |

**Styles:**
```xml
<Style Selector="TextBlock.H1">
    <Setter Property="FontSize" Value="24"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
</Style>

<Style Selector="TextBlock.H2">
    <Setter Property="FontSize" Value="18"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
</Style>

<Style Selector="TextBlock.Body">
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="Regular"/>
</Style>

<Style Selector="TextBlock.Caption">
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Foreground" Value="{StaticResource SecondaryTextBrush}"/>
</Style>
```

**Usage:**
```xml
<TextBlock Classes="H1" Text="Title"/>
<TextBlock Classes="Body" Text="Main text"/>
<TextBlock Classes="Caption" Text="Caption"/>
```

---

## 🔘 Button Styles

### Primary Button

```xml
<Style Selector="Button.Primary">
    <Setter Property="Background" Value="{DynamicResource SystemAccentBrush}"/>
    <Setter Property="Foreground" Value="#FFFFFF"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="24,12"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Cursor" Value="Hand"/>
</Style>

<Style Selector="Button.Primary:pointerover">
    <Setter Property="Opacity" Value="0.9"/>
</Style>

<Style Selector="Button.Primary:pressed">
    <Setter Property="Opacity" Value="0.8"/>
    <Setter Property="RenderTransform">
        <TranslateTransform Y="1"/>
    </Setter>
</Style>

<Style Selector="Button.Primary:disabled">
    <Setter Property="Opacity" Value="0.5"/>
</Style>
```

### Secondary Button

```xml
<Style Selector="Button.Secondary">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource SecondaryTextBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="16,8"/>
    <Setter Property="CornerRadius" Value="6"/>
</Style>
```

### Icon Button

```xml
<Style Selector="Button.Icon">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Width" Value="40"/>
    <Setter Property="Height" Value="40"/>
    <Setter Property="CornerRadius" Value="20"/>
    <Setter Property="Padding" Value="8"/>
</Style>

<Style Selector="Button.Icon:pointerover">
    <Setter Property="Background" Value="{StaticResource BackgroundHoverBrush}"/>
</Style>
```

---

## 📦 Card Styles

### Basic Card

```xml
<Style Selector="Border.Card">
    <Setter Property="Background" Value="{StaticResource BackgroundLightBrush}"/>
    <Setter Property="CornerRadius" Value="12"/>
    <Setter Property="Padding" Value="16"/>
    <Setter Property="BoxShadow" Value="0 2 8 0 #20000000"/>
</Style>

<Style Selector="Border.Card:pointerover">
    <Setter Property="Background" Value="{StaticResource BackgroundHoverBrush}"/>
</Style>
```

### Usage

```xml
<Border Classes="Card">
    <StackPanel Spacing="8">
        <TextBlock Classes="H3" Text="Card Title"/>
        <TextBlock Classes="Body" Text="Card content"/>
    </StackPanel>
</Border>
```

---

## 📝 Input Styles

### TextBox

```xml
<Style Selector="TextBox">
    <Setter Property="Background" Value="{StaticResource BackgroundMediumBrush}"/>
    <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BackgroundHoverBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="6"/>
    <Setter Property="Padding" Value="12,8"/>
    <Setter Property="FontSize" Value="14"/>
</Style>

<Style Selector="TextBox:focus">
    <Setter Property="BorderBrush" Value="{DynamicResource SystemAccentBrush}"/>
</Style>

<Style Selector="TextBox:error">
    <Setter Property="BorderBrush" Value="{StaticResource ErrorBrush}"/>
</Style>
```

### ComboBox

```xml
<Style Selector="ComboBox">
    <Setter Property="Background" Value="{StaticResource BackgroundMediumBrush}"/>
    <Setter Property="CornerRadius" Value="6"/>
    <Setter Property="Padding" Value="12,8"/>
</Style>
```

---

## 🔲 Borders and Spacing

### Spacing Guidelines

| Name | Value | Usage |
|------|-------|-------|
| xs | 4px | Inside compact elements |
| sm | 8px | Between related elements |
| md | 16px | Between sections |
| lg | 24px | Large dividers |
| xl | 32px | Between major blocks |

### Border Radius

| Size | Value | Usage |
|------|-------|-------|
| Small | 4px | Small elements (chips, tags) |
| Medium | 8px | Buttons, inputs |
| Large | 12px | Cards |
| XL | 16px | Modal windows |
| Round | 50% | Round elements (avatars) |

---

## 🎭 Animations

### Timing Functions

```xml
<!-- Standard animation -->
<Animation Duration="0:0:0.2" Easing="CubicEaseOut"/>

<!-- Fast animation (hover) -->
<Animation Duration="0:0:0.15" Easing="CubicEaseOut"/>

<!-- Slow animation (appearance) -->
<Animation Duration="0:0:0.3" Easing="CubicEaseInOut"/>
```

### Fade In

```xml
<Style Selector="Panel.FadeIn">
    <Style.Animations>
        <Animation Duration="0:0:0.3" Easing="CubicEaseOut">
            <KeyFrame Cue="0%">
                <Setter Property="Opacity" Value="0"/>
            </KeyFrame>
            <KeyFrame Cue="100%">
                <Setter Property="Opacity" Value="1"/>
            </KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

### Slide In

```xml
<Style Selector="Panel.SlideIn">
    <Style.Animations>
        <Animation Duration="0:0:0.3" Easing="CubicEaseOut">
            <KeyFrame Cue="0%">
                <Setter Property="Opacity" Value="0"/>
                <Setter Property="TranslateTransform.Y" Value="20"/>
            </KeyFrame>
            <KeyFrame Cue="100%">
                <Setter Property="Opacity" Value="1"/>
                <Setter Property="TranslateTransform.Y" Value="0"/>
            </KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

### Scale

```xml
<Style Selector="Button:pointerover">
    <Setter Property="RenderTransform">
        <ScaleTransform ScaleX="1.02" ScaleY="1.02"/>
    </Setter>
</Style>
```

---

## 🖼️ Icons

### SVG Icons

All icons are stored in `Assets/Icons/`:

```
Assets/Icons/
├── play.svg
├── pause.svg
├── settings.svg
├── user.svg
├── mod.svg
├── download.svg
├── check.svg
├── close.svg
├── arrow-left.svg
├── arrow-right.svg
└── Flags/          # Flags for language selection
    ├── en-US.svg
    ├── ru-RU.svg
    └── ...
```

### Usage

```xml
<svg:Svg Path="/Assets/Icons/play.svg" 
         Width="24" 
         Height="24"/>
```

### Accent Color for Icons

```xml
<!-- Via style -->
<svg:Svg Path="/Assets/Icons/star.svg">
    <svg:Svg.Styles>
        <Style Selector="Path">
            <Setter Property="Fill" Value="{DynamicResource SystemAccentBrush}"/>
        </Style>
    </svg:Svg.Styles>
</svg:Svg>
```

---

## 📱 Responsive Design

### Breakpoints

HyPrism supports adaptive design:

| Name | Width | Usage |
|------|-------|-------|
| Compact | < 800px | Collapsed sidebar |
| Normal | 800-1200px | Standard layout |
| Wide | > 1200px | Expanded layout |

### Adaptive Triggers

```xml
<Grid>
    <Grid.Styles>
        <Style Selector="Grid.Sidebar">
            <Setter Property="Width" Value="280"/>
        </Style>
        
        <!-- When window width < 800 -->
        <Style Selector="Grid.Sidebar[(IsCompact)=True]">
            <Setter Property="Width" Value="60"/>
        </Style>
    </Grid.Styles>
</Grid>
```

---

## ✅ Styling Checklist

When adding a new component, check:

- [ ] Uses `StaticResource` or `DynamicResource`
- [ ] No hardcoded colors
- [ ] Follows spacing guidelines
- [ ] Has `:pointerover`, `:pressed`, `:disabled` states
- [ ] Smooth animations (at least 150ms)
- [ ] Fonts from typography palette
- [ ] Icons in SVG format
- [ ] Works with system accent color

---

## 📚 Additional Resources

- [UIComponentGuide.md](UIComponentGuide.md) — Creating Components
- [Avalonia Styling](https://docs.avaloniaui.net/docs/styling/) — Avalonia Documentation
