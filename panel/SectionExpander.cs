using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace AmtPtpControlPanel.WinUI3;

public sealed class SectionExpander : Expander
{
    private ToggleButton? header;
    private Brush? restingBrush;

    protected override void OnApplyTemplate()
    {
        if (header != null)
        {
            header.PointerEntered -= OnHeaderEntered;
            header.PointerExited -= OnHeaderExited;
        }

        base.OnApplyTemplate();
        header = GetTemplateChild("ExpanderHeader") as ToggleButton;
        if (header == null) return;
        header.PointerEntered += OnHeaderEntered;
        header.PointerExited += OnHeaderExited;
    }

    private void OnHeaderEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (header == null || restingBrush != null) return;
        restingBrush = header.Background;
        var foreground = new UISettings().GetColorValue(UIColorType.Foreground);
        header.Background = new SolidColorBrush(foreground) { Opacity = 0.16 };
    }

    private void OnHeaderExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (header == null || restingBrush == null) return;
        header.Background = restingBrush;
        restingBrush = null;
    }
}
