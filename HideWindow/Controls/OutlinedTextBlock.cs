using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace HideWindow.Controls;

/// <summary>
/// 支持描边的文本控件:把文字转换为几何图形,用前景色填充、描边色勾勒,
/// 适用于叠加在背景图片上的文字。
/// </summary>
public class OutlinedTextBlock : FrameworkElement
{
    private Geometry? _geometry;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGeometryInvalidated));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(1.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGeometryInvalidated));

    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextWrapping.NoWrap,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGeometryInvalidated));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
            FrameworkPropertyMetadataOptions.Inherits,
            OnGeometryInvalidated));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
            FrameworkPropertyMetadataOptions.Inherits,
            OnGeometryInvalidated));

    public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontStyle,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
            FrameworkPropertyMetadataOptions.Inherits,
            OnGeometryInvalidated));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontWeight,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
            FrameworkPropertyMetadataOptions.Inherits,
            OnGeometryInvalidated));

    public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(FontStretches.Normal,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
            FrameworkPropertyMetadataOptions.Inherits,
            OnGeometryInvalidated));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextAlignment.Left,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGeometryInvalidated));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStretch FontStretch
    {
        get => (FontStretch)GetValue(FontStretchProperty);
        set => SetValue(FontStretchProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    private static void OnGeometryInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((OutlinedTextBlock)d)._geometry = null;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGeometry(availableSize);
        if (_geometry == null)
            return new Size(0, 0);

        Rect bounds = _geometry.Bounds;
        return new Size(bounds.Right + StrokeThickness, bounds.Bottom + StrokeThickness);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        EnsureGeometry(RenderSize);
        if (_geometry == null) return;

        // 先画描边、再画实心填充:保证小字号下笔画内部是纯前景色,外圈是描边。
        drawingContext.DrawGeometry(null, new Pen(Stroke, StrokeThickness), _geometry);
        drawingContext.DrawGeometry(Foreground, null, _geometry);
    }

    private void EnsureGeometry(Size availableSize)
    {
        if (_geometry != null) return;

        string text = Text ?? string.Empty;
        if (text.Length == 0) return;

        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // BuildGeometry 要求段落宽度/高度必须有限。
        double maxWidth;
        double maxHeight;
        if (TextWrapping == TextWrapping.Wrap)
        {
            // 显式换行:按可用宽度排布,无限约束时退化为文本自然尺寸。
            maxWidth = availableSize.Width;
            maxHeight = availableSize.Height;
            if (double.IsInfinity(maxWidth) || maxWidth <= 0)
                maxWidth = formattedText.Width + StrokeThickness;
            else
                maxWidth = Math.Max(0, maxWidth - StrokeThickness);
            if (double.IsInfinity(maxHeight) || maxHeight <= 0)
                maxHeight = formattedText.Height + StrokeThickness;
            else
                maxHeight = Math.Max(0, maxHeight - StrokeThickness);
        }
        else if (TextAlignment == TextAlignment.Left)
        {
            // 不换行(默认):按自然宽度排版,避免在窄约束下被堆叠换行。
            maxWidth = formattedText.Width + StrokeThickness;
            maxHeight = formattedText.Height + StrokeThickness;
        }
        else
        {
            // 不换行但需要右对齐:按可用宽度排版以保留对齐效果。
            maxWidth = availableSize.Width;
            if (double.IsInfinity(maxWidth) || maxWidth <= 0)
                maxWidth = formattedText.Width + StrokeThickness;
            else
                maxWidth = Math.Max(0, maxWidth - StrokeThickness);
            maxHeight = formattedText.Height + StrokeThickness;
        }

        formattedText.MaxTextWidth = maxWidth;
        formattedText.MaxTextHeight = maxHeight;
        formattedText.TextAlignment = TextAlignment;

        _geometry = formattedText.BuildGeometry(new Point(0, 0));
    }
}
