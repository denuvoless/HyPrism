# Руководство по стилизации

> Полное руководство по теме, цветовой палитре, стилям и визуальному дизайну HyPrism.

---

## Содержание

- [Система тем](#-система-тем)
- [Цветовая палитра](#-цветовая-палитра)
- [Типографика](#-типографика)
- [Иконки](#-иконки)
- [Стили кнопок](#-стили-кнопок)
- [Поля ввода](#-поля-ввода)
- [Карточки и контейнеры](#-карточки-и-контейнеры)
- [Анимации](#-анимации)
- [Адаптивный дизайн](#-адаптивный-дизайн)
- [Чеклист](#-чеклист)

---

## 🎨 Система тем

HyPrism использует единую тёмную тему с настраиваемым акцентным цветом.

### ThemeService

**Файл:** `Services/Core/ThemeService.cs`

```csharp
public class ThemeService
{
    public static ThemeService Instance { get; }
    
    public void Initialize(string hexColor);
    public void ApplyAccentColor(string hexColor);
}
```

**Инициализация (App.axaml.cs):**
```csharp
ThemeService.Instance.Initialize(config.AccentColor ?? "#FFA845");
```

**Изменение акцентного цвета:**
```csharp
ThemeService.Instance.ApplyAccentColor("#FF5500");
```

---

## 🎨 Цветовая палитра

### Базовые цвета

Определены в `UI/Styles/SharedColors.axaml`:

```xml
<ResourceDictionary>
    <!-- Фоновые цвета -->
    <Color x:Key="BackgroundDark">#121212</Color>
    <Color x:Key="BackgroundMedium">#1E1E1E</Color>
    <Color x:Key="BackgroundLight">#2A2A2A</Color>
    <Color x:Key="BackgroundHover">#333333</Color>
    
    <!-- Текстовые цвета -->
    <Color x:Key="TextPrimary">#FFFFFF</Color>
    <Color x:Key="TextSecondary">#B0B0B0</Color>
    <Color x:Key="TextDisabled">#666666</Color>
    
    <!-- Акцентный цвет (динамический) -->
    <Color x:Key="SystemAccentColor">#FFA845</Color>
    
    <!-- Статусные цвета -->
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

### Использование цветов

```xml
<!-- Через StaticResource (статические цвета) -->
<Border Background="{StaticResource BackgroundMediumBrush}"/>
<TextBlock Foreground="{StaticResource PrimaryTextBrush}"/>

<!-- Через DynamicResource (динамические цвета, например акцент) -->
<Button Background="{DynamicResource SystemAccentBrush}"/>
```

---

## 📝 Типографика

### Шрифты

HyPrism использует системные шрифты с fallback:

```xml
<Application.Resources>
    <FontFamily x:Key="DefaultFontFamily">
        Segoe UI, SF Pro Display, -apple-system, 
        Noto Sans, sans-serif
    </FontFamily>
</Application.Resources>
```

### Размеры текста

| Назначение | Размер | Вес |
|------------|--------|-----|
| H1 (Заголовок) | 24px | SemiBold |
| H2 (Подзаголовок) | 18px | SemiBold |
| H3 (Секция) | 16px | Medium |
| Body (Текст) | 14px | Regular |
| Caption (Подпись) | 12px | Regular |
| Micro (Мелкий) | 10px | Regular |

**Стили:**
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

**Использование:**
```xml
<TextBlock Classes="H1" Text="Заголовок"/>
<TextBlock Classes="Body" Text="Основной текст"/>
<TextBlock Classes="Caption" Text="Подпись"/>
```

---

## 🔘 Стили кнопок

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

## 📦 Стили карточек

### Базовая карточка

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

### Использование

```xml
<Border Classes="Card">
    <StackPanel Spacing="8">
        <TextBlock Classes="H3" Text="Заголовок карточки"/>
        <TextBlock Classes="Body" Text="Содержимое карточки"/>
    </StackPanel>
</Border>
```

---

## 📝 Стили полей ввода

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

## 🔲 Границы и отступы

### Spacing Guidelines

| Название | Значение | Использование |
|----------|----------|---------------|
| xs | 4px | Внутри компактных элементов |
| sm | 8px | Между связанными элементами |
| md | 16px | Между секциями |
| lg | 24px | Большие разделители |
| xl | 32px | Между крупными блоками |

### Border Radius

| Размер | Значение | Использование |
|--------|----------|---------------|
| Small | 4px | Мелкие элементы (chips, tags) |
| Medium | 8px | Кнопки, инпуты |
| Large | 12px | Карточки |
| XL | 16px | Модальные окна |
| Round | 50% | Круглые элементы (аватары) |

---

## 🎭 Анимации

### Timing Functions

```xml
<!-- Стандартная анимация -->
<Animation Duration="0:0:0.2" Easing="CubicEaseOut"/>

<!-- Быстрая анимация (hover) -->
<Animation Duration="0:0:0.15" Easing="CubicEaseOut"/>

<!-- Медленная анимация (появление) -->
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

## 🖼️ Иконки

### SVG иконки

Все иконки хранятся в `Assets/Icons/`:

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
└── Flags/          # Флаги для выбора языка
    ├── en-US.svg
    ├── ru-RU.svg
    └── ...
```

### Использование

```xml
<svg:Svg Path="/Assets/Icons/play.svg" 
         Width="24" 
         Height="24"/>
```

### Акцентный цвет для иконок

```xml
<!-- Через стиль -->
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

HyPrism поддерживает адаптивный дизайн:

| Название | Ширина | Использование |
|----------|--------|---------------|
| Compact | < 800px | Свёрнутая боковая панель |
| Normal | 800-1200px | Стандартный лейаут |
| Wide | > 1200px | Расширенный лейаут |

### Adaptive Triggers

```xml
<Grid>
    <Grid.Styles>
        <Style Selector="Grid.Sidebar">
            <Setter Property="Width" Value="280"/>
        </Style>
        
        <!-- При ширине окна < 800 -->
        <Style Selector="Grid.Sidebar[(IsCompact)=True]">
            <Setter Property="Width" Value="60"/>
        </Style>
    </Grid.Styles>
</Grid>
```

---

## ✅ Чеклист стилизации

При добавлении нового компонента проверьте:

- [ ] Используются `StaticResource` или `DynamicResource`
- [ ] Нет hardcoded цветов
- [ ] Соблюдается spacing guidelines
- [ ] Добавлены состояния `:pointerover`, `:pressed`, `:disabled`
- [ ] Анимации плавные (не менее 150ms)
- [ ] Шрифты из палитры типографики
- [ ] Иконки в формате SVG
- [ ] Работает с системным акцентным цветом

---

## 📚 Дополнительные ресурсы

- [UIComponentGuide.md](UIComponentGuide.md) — Создание компонентов
- [Avalonia Styling](https://docs.avaloniaui.net/docs/styling/) — Документация Avalonia
