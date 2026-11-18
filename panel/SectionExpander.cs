using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml.Media.Animation;

namespace AmtPtpControlPanel.WinUI3;

public sealed class SectionExpander : Expander
{
    private ToggleButton? header;
    private Brush? restingBrush;

    private Border? contentClip;
    private Storyboard? closing;
    private static readonly TimeSpan CloseDuration=TimeSpan.FromMilliseconds(200);

    public SectionExpander()
    {
        Collapsed += OnCollapsed;
        Expanding += (_,_) => StopClosing();
        Unloaded += (_,_) => StopClosing();
    }

    private void StopClosing()
    {
        closing?.Stop();
        closing=null;
        contentClip?.ClearValue(HeightProperty);
    }

    private void OnCollapsed(Expander sender, ExpanderCollapsedEventArgs args)
    {
        if(contentClip is null || contentClip.ActualHeight<=0 || !new UISettings().AnimationsEnabled)return;
        var motion=new DoubleAnimation {
            From=contentClip.ActualHeight, To=0, Duration=CloseDuration,
            EnableDependentAnimation=true,
            EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseOut }
        };
        var storyboard=new Storyboard();
        Storyboard.SetTarget(motion,contentClip);
        Storyboard.SetTargetProperty(motion,"Height");
        storyboard.Children.Add(motion);
        // Hold zero until the next expansion so the last frame cannot jump back to Auto.
        closing=storyboard;
        storyboard.Begin();
    }

    protected override void OnApplyTemplate()
    {
        if (header != null)
        {
            header.PointerEntered -= OnHeaderEntered;
            header.PointerExited -= OnHeaderExited;
        }

        StopClosing();
        base.OnApplyTemplate();
        contentClip=GetTemplateChild("ExpanderContentClip") as Border;
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
