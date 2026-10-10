using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NexusDeviceService.Desktop.Controls;

public partial class SummaryCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SummaryCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(SummaryCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtextProperty =
        DependencyProperty.Register(nameof(Subtext), typeof(string), typeof(SummaryCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconGeometryProperty =
        DependencyProperty.Register(nameof(IconGeometry), typeof(Geometry), typeof(SummaryCard), new PropertyMetadata(null));

    public static readonly DependencyProperty IconBrushProperty =
        DependencyProperty.Register(nameof(IconBrush), typeof(Brush), typeof(SummaryCard), new PropertyMetadata(null));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(SummaryCard), new PropertyMetadata(null));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(nameof(BadgeText), typeof(string), typeof(SummaryCard), new PropertyMetadata(string.Empty, OnBadgeChanged));

    public static readonly DependencyProperty BadgeBrushProperty =
        DependencyProperty.Register(nameof(BadgeBrush), typeof(Brush), typeof(SummaryCard), new PropertyMetadata(null));

    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(SummaryCard), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Subtext
    {
        get => (string)GetValue(SubtextProperty);
        set => SetValue(SubtextProperty, value);
    }

    public Geometry? IconGeometry
    {
        get => (Geometry?)GetValue(IconGeometryProperty);
        set => SetValue(IconGeometryProperty, value);
    }

    public Brush? IconBrush
    {
        get => (Brush?)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public Brush? ValueBrush
    {
        get => (Brush?)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public Brush? BadgeBrush
    {
        get => (Brush?)GetValue(BadgeBrushProperty);
        set => SetValue(BadgeBrushProperty, value);
    }

    public Brush? BadgeBackground
    {
        get => (Brush?)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public SummaryCard()
    {
        InitializeComponent();
        UpdateBadgeVisibility();
    }

    private static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SummaryCard card)
        {
            card.UpdateBadgeVisibility();
        }
    }

    private void UpdateBadgeVisibility()
    {
        if (badgeBorder != null)
        {
            badgeBorder.Visibility = string.IsNullOrWhiteSpace(BadgeText) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
