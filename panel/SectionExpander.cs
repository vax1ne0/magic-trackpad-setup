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
    private static readonly TimeSpan CloseDuration=TimeSpan.FromMilliseconds(300);

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
            EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut }
        };
        var storyboard=new Storyboard();
        Storyboard.SetTarget(motion,contentClip);
        Storyboard.SetTargetProperty(motion,"Height");
        storyboard.Children.Add(motion);
        storyboard.Completed+=(_,_)=> { if(ReferenceEquals(closing,storyboard))StopClosing(); };
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
        // Keep WinUI's slide and visibility change in step with the shrinking row.
        if(contentClip?.Parent is FrameworkElement root && new UISettings().AnimationsEnabled)
        {
            foreach(var group in VisualStateManager.GetVisualStateGroups(root))
                foreach(var state in group.States)
                    if(state.Name=="CollapseUp" && state.Storyboard is Storyboard slide)
                        slide.SpeedRatio=167.0/CloseDuration.TotalMilliseconds;
        }
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
