using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
namespace AmtPtpControlPanel.WinUI3;

public sealed partial class MainWindow
{
    readonly Surface surface=new();
    readonly KeyOutput keyOutput=new();
    readonly Stopwatch inputClock=Stopwatch.StartNew();
    readonly Dictionary<StickerKey,Border> keyViews=new();
    TouchInput? touchInput;
    SwipeGuard? swipeGuard;
    bool syncingInput=true;
    nint inputWindow;
    [DllImport("user32")] static extern nint GetForegroundWindow();

    readonly DispatcherTimer cornerTimer = new() { Interval=TimeSpan.FromMilliseconds(Surface.PositionHoldMs) };
    readonly DispatcherTimer repeatTimer = new();
    nint repeatTarget;
    TimeSpan repeatInterval;
    [DllImport("user32", EntryPoint = "SystemParametersInfoW")]
    static extern bool GetKeyboardSetting(uint action, uint unused, out uint value, uint flags);

    void SetRepeat(bool held)
    {
        repeatTimer.Stop();
        if (!held || !keyOutput.CanRepeat) return;
        repeatTarget = GetForegroundWindow();
        var delay = GetKeyboardSetting(0x0016, 0, out var d, 0) ? Math.Min(d, 3u) : 1u;
        var speed = GetKeyboardSetting(0x000A, 0, out var s, 0) ? Math.Min(s, 31u) : 31u;
        repeatInterval = TimeSpan.FromMilliseconds(1000 / (2.5 + speed * 27.5 / 31));
        repeatTimer.Interval = TimeSpan.FromMilliseconds(250 * (1 + delay));
        repeatTimer.Start();
    }

    void InitializeInput()
    {
        OverlayTransparency.Value=Math.Clamp(AppPreferences.ReadInt("OverlayTransparency",KeyboardOverlay.DefaultTransparency),0,90);
        OverlayScale.Value=Math.Clamp(AppPreferences.ReadInt("OverlayScale",KeyboardOverlay.DefaultScale),50,150);
        OverlayButton.IsChecked=AppPreferences.Enabled("FloatingKeyboard",true);
        FadeIdle.IsOn=AppPreferences.Enabled("OverlayIdleFade",true);
        IdleAmount.Value=Math.Clamp(AppPreferences.ReadInt("OverlayIdleAmount",KeyboardOverlay.DefaultIdleDimming),10,95);
        IdleDuration.Value=Math.Clamp(AppPreferences.ReadInt("OverlayIdleDuration",KeyboardOverlay.DefaultFadeDurationMs),200,2000);
        UpdateAppearanceLabels();
        inputWindow=WindowNative.GetWindowHandle(this);
        KeyboardSwitch.IsOn=AppPreferences.Enabled("StickerEnabled",false);
        TapSwitch.IsChecked=AppPreferences.Enabled("StickerTap",false);
        CursorGesture.IsChecked=surface.TwoFingerPointer=AppPreferences.Enabled("TwoFingerPointer",false);
        EdgeSwitch.IsOn=surface.EdgeSliders=AppPreferences.Enabled("EdgeSliders",false);
        var edgeLevels=new EdgeLevels(message=>DispatcherQueue.TryEnqueue(()=>
        {
            if(!surface.EdgeSliders)return;
            EdgeStatus.Text=message;
            EdgeStatus.Visibility=Visibility.Visible;
        }));
        surface.EdgeLevel=edgeLevels.Set;
        MediaSwitch.IsOn=AppPreferences.Enabled("MediaGestures",false);
        surface.ModeEnabled=KeyboardSwitch.IsOn;
        surface.Keyboard=false;
        surface.TapMode=TapSwitch.IsChecked==true;
        InitializeGestureRows();
        parking=new PointerParking(ExitKeyboardMode);
        surface.EdgeContactChanged=parking.HoldEdgeCursor;
        surface.NormalPointerSample=parking.ObserveNormalPointer;
        surface.PointerButton=parking.SetLeftButton;
        surface.PointerScroll=parking.Scroll;
        surface.PointerMoved=(dx,dy)=>parking?.MovePointer(dx,dy);
        surface.PointerChanged=(active,tap)=>
        {
            if(parking?.Active==true)parking.SetPointing(active,tap);
        };
        surface.Emit=EmitInput;
        surface.RepeatChanged = SetRepeat;
        repeatTimer.Tick += (_, _) =>
        {
            if (repeatTarget == 0 || GetForegroundWindow() != repeatTarget || !keyOutput.Repeat())
                repeatTimer.Stop();
            else if (repeatTimer.Interval != repeatInterval)
                repeatTimer.Interval = repeatInterval;
        };
        surface.CornerTimer=armed=>
        {
            cornerTimer.Stop();
            if(armed)cornerTimer.Start();
        };
        cornerTimer.Tick+=(_,_)=>surface.CheckCornerHold(inputClock.ElapsedMilliseconds);
        surface.ModeChanged=()=>
        {
            if(surface.Positioning && OverlayButton.IsChecked!=true)
            {
                OverlayButton.IsChecked=true;
                AppPreferences.Set("FloatingKeyboard",true);
            }
            ApplyInputMode();
        };
        foreach(var key in surface.Keys){
            var b=key.Bounds;
            var border=new Border { Width=b.Width*4,Height=b.Height*4,CornerRadius=new CornerRadius(4),
                BorderThickness=new Thickness(1),BorderBrush=(Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Child=new TextBlock {Text=key.Label,FontSize=key.Label.Length>4?11:14,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
            Canvas.SetLeft(border,b.X*4);Canvas.SetTop(border,b.Y*4);
            Sticker.Children.Add(border);keyViews.Add(key,border);
        }
        syncingInput=false;
        UpdateReceiver();
        Activated+=(_,_)=>{if(!surface.PointingOrScrolling)ResetInput(keepModeContact:true);};
        AppWindow.Closing+=(_,args)=>
        {
            if((!surface.ModeEnabled && !surface.EdgeSliders) || exitingPanel)return;
            args.Cancel=true;
            if(surface.Positioning){surface.Positioning=false;ApplyInputMode();}
            string tray=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"..","TrackpadBattery.exe"));
            if(System.IO.File.Exists(tray))
                Process.Start(new ProcessStartInfo(tray,"--resume"){UseShellExecute=true})?.Dispose();
            AppWindow.Hide();
        };
        Closed+=(_,_)=>{edgeLevels.Dispose();StopTabSlide();ResetInput();touchInput?.Dispose();swipeGuard?.Dispose();overlay?.Dispose();parking?.Dispose();};
    }
    void InitializeGestureRows()
    {
        string[] actions = ["Nothing", "Play / pause", "Previous track", "Next track", "Volume up", "Volume down"];
        foreach (var gesture in Enum.GetValues<Surface.Gesture>())
        {
            string label = gesture == Surface.Gesture.Tap ? "Tap" : "Swipe " + gesture.ToString().ToLowerInvariant();
            string preference = "FiveFinger" + gesture;
            string saved = AppPreferences.ReadText(preference, surface.Actions[gesture]);
            if (actions.Contains(saved)) surface.Actions[gesture] = saved;

            var row = new Grid { Padding = new Thickness(20, 8, 20, 8), ColumnSpacing = 12, MinHeight = 52 };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var picker = new ComboBox { Width = 154, ItemsSource = actions, SelectedItem = surface.Actions[gesture],
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(picker, label);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(picker, preference);
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedItem is not string action) return;
                surface.Actions[gesture] = action;
                AppPreferences.Set(preference, action);
            };
            Grid.SetColumn(picker, 1);
            row.Children.Add(picker);
            GestureRows.Children.Add(new Border { Child = row, BorderThickness = new Thickness(0, GestureRows.Children.Count == 0 ? 0 : 1, 0, 0),
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] });
        }
    }

    bool KeyboardSelected => Pages.SelectedItem == KeyboardTab;
    KeyboardOverlay? overlay;
    PointerParking? parking;
    bool deviceReady;

    void ApplyInputMode()
    {
        if(surface.Positioning)ResetInput(keepModeContact:true);
        bool typing = surface.Keyboard && deviceReady && !surface.Positioning;
        UpdateOverlayVisibility();
        if (!typing)
        {
            parking?.Release();
            return;
        }
        if(parking==null)return;
        if (!parking.Engage())
        {
            ExitKeyboardMode();
            ShowKeyboardError("Pointer control is unavailable. Check whether another app uses Ctrl+Alt+F10.");
        }
    }


    void ExitKeyboardMode()
    {
        surface.Keyboard=false;
        surface.Positioning=false;
        ResetInput();
        ApplyInputMode();
    }

    void UpdateAppearanceLabels()
    {
        TransparencyValue.Text=$"{OverlayTransparency.Value:0}%";
        IdleAmountValue.Text=$"{IdleAmount.Value:0}%";
        IdleDurationValue.Text=$"{IdleDuration.Value/1000:0.##} s";
        ScaleValue.Text=$"{OverlayScale.Value:0}%";
    }

    void OverlayAppearanceChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if(syncingInput)return;
        UpdateAppearanceLabels();
        AppPreferences.Set(ReferenceEquals(sender,OverlayScale)?"OverlayScale":"OverlayTransparency",(int)e.NewValue);
        overlay?.SetAppearance((int)OverlayScale.Value,(int)OverlayTransparency.Value);
    }

    void IdleAppearanceChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if(syncingInput)return;
        AppPreferences.Set(ReferenceEquals(sender,IdleAmount)?"OverlayIdleAmount":"OverlayIdleDuration",(int)e.NewValue);
        UpdateAppearanceLabels();
        ApplyIdleAppearance();
    }

    void ApplyIdleAppearance() => overlay?.SetIdleFade(FadeIdle.IsOn,(int)IdleAmount.Value,(int)IdleDuration.Value);

    void FadeIdleChanged(object sender,RoutedEventArgs e)
    {
        if(syncingInput)return;
        AppPreferences.Set("OverlayIdleFade",FadeIdle.IsOn);
        ApplyIdleAppearance();
    }

    void CreateOverlay()
    {
        var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        var color = ((SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"]).Color;
        overlay = new KeyboardOverlay(surface.Keys, display.WorkArea, RootGrid.XamlRoot.RasterizationScale, dark,
            (uint)(color.R | color.G << 8 | color.B << 16),(int)OverlayScale.Value,(int)OverlayTransparency.Value);
        overlay.SetGestureDimmed(surface.PointingOrScrolling);
        overlay.Highlight(surface.Hover,surface.ModifierContact);
        overlay.SetTouching(surface.HasContacts);
        ApplyIdleAppearance();
    }

    void ToggleOverlay(object sender, RoutedEventArgs e)
    {
        AppPreferences.Set("FloatingKeyboard",OverlayButton.IsChecked==true);
        if (OverlayButton.IsChecked != true)
        {
            overlay?.Dispose();
            overlay=null;
            surface.Positioning=false;
        }
        ApplyInputMode();
        UpdateReceiver();
    }

    void UpdateOverlayVisibility()
    {
        bool enabled=OverlayButton.IsChecked==true;
        bool canPosition = (!surface.Keyboard || surface.Positioning) && KeyboardSelected && enabled;
        PositionButton.Visibility = canPosition ? Visibility.Visible : Visibility.Collapsed;
        PositionButton.IsChecked=surface.Positioning;
        if(enabled && overlay==null && (surface.Positioning || (surface.Keyboard && deviceReady)))CreateOverlay();
        overlay?.SetPositioning(surface.Positioning);
        overlay?.SetVisible((surface.Keyboard && deviceReady) || surface.Positioning);
    }

    void PositionOverlay(object sender,RoutedEventArgs e)
    {
        surface.Positioning=PositionButton.IsChecked==true;
        ApplyInputMode();
    }

    SelectorBarItem? indicatedTab;
    double indicatorTarget=double.NaN;
    readonly Stopwatch tabSlide=new();
    double slideFrom, initialStretch;

    void StopTabSlide()
    {
        if(!tabSlide.IsRunning)return;
        CompositionTarget.Rendering-=AnimateTabIndicator;
        tabSlide.Reset();
    }

    void AnimateTabIndicator(object? sender,object e)
    {
        // The leading edge reaches first. Interpolate from the currently drawn
        // shape so reversing direction mid-slide starts at the same two edges.
        double ms=tabSlide.Elapsed.TotalMilliseconds*1.25;
        double distance=indicatorTarget-slideFrom;
        double direction=Math.Sign(distance);
        double reach=Math.Min(10,Math.Abs(distance)*0.15);
        double settle=Math.Min(2.4,Math.Abs(distance)*0.04);
        double pull=Ease(ms/150);
        double glide=Ease((ms-150)/310);
        double recoil=Ease((ms-460)/105);
        double rest=Ease((ms-565)/95);

        double extension=reach*pull*(1-glide);
        TabMotion.ScaleX=1+(initialStretch-1)*(1-glide)+extension/TabIndicator.Width;
        TabMotion.TranslateX=slideFrom+distance*glide+direction*(extension/2
            +settle*(glide-1.35*recoil+0.35*rest));
        if(ms>=660)StopTabSlide();

        static double Ease(double t)
        {
            t=Math.Clamp(t,0,1);
            return t*t*(3-2*t);
        }
    }

    void TabLayoutUpdated(object? sender, object e) => UpdateTabIndicator();

    void UpdateTabIndicator()
    {
        if(TabMotion==null || Pages.SelectedItem is not SelectorBarItem tab || tab.ActualWidth<=0)return;
        // Measure in the overlay's coordinates so label widths and RTL layouts work alike.
        var center=tab.TransformToVisual(TabIndicatorLayer).TransformPoint(
            new Windows.Foundation.Point(tab.ActualWidth/2,tab.ActualHeight));
        double target=center.X-TabIndicator.Width/2;
        double top=center.Y-TabIndicator.Height;
        if(Canvas.GetTop(TabIndicator)!=top)Canvas.SetTop(TabIndicator,top);
        if(tab==indicatedTab && Math.Abs(target-indicatorTarget)<0.1)return;

        bool switched=indicatedTab!=null && indicatedTab!=tab;
        StopTabSlide();
        indicatedTab=tab;
        indicatorTarget=target;
        TabIndicator.Opacity=1;
        if(switched && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            slideFrom=TabMotion.TranslateX;
            initialStretch=TabMotion.ScaleX;
            tabSlide.Restart();
            CompositionTarget.Rendering+=AnimateTabIndicator;
        }
        else
        {
            TabMotion.TranslateX=target;
            TabMotion.ScaleX=1;
        }
    }

    void PageChanged(SelectorBar sender,SelectorBarSelectionChangedEventArgs e)
    {
        if(SettingsFooter==null)return;
        UpdateTabIndicator();
        TrackpadPage.Visibility=KeyboardSelected?Visibility.Collapsed:Visibility.Visible;
        KeyboardPage.Visibility=KeyboardSelected?Visibility.Visible:Visibility.Collapsed;
        SettingsFooter.Visibility=!KeyboardSelected?Visibility.Visible:Visibility.Collapsed;
        ResetInput();
        if(!syncingInput){UpdateOverlayVisibility();UpdateReceiver();}
    }
    void ResetInput(bool keepModeContact=false)
    {
        if(surface.Hover!=null && keyViews.TryGetValue(surface.Hover,out var b))b.Background=null;
        if(surface.ModifierContact!=null)keyViews[surface.ModifierContact].Background=null;
        overlay?.Highlight(null);
        overlay?.SetGestureDimmed(false);
        overlay?.SetTouching(false);
        surface.Reset(keepModeContact);keyOutput.Reset();
    }
    void InputOptionChanged(object sender,RoutedEventArgs e)
    {
        if(syncingInput)return;
        ResetInput();
        surface.ModeEnabled=KeyboardSwitch.IsOn;
        if(!surface.ModeEnabled){surface.Keyboard=false;surface.Positioning=false;}
        surface.TapMode=TapSwitch.IsChecked==true;
        AppPreferences.Set("StickerEnabled",surface.ModeEnabled);AppPreferences.Set("StickerTap",surface.TapMode);
        surface.TwoFingerPointer=CursorGesture.IsChecked==true;
        AppPreferences.Set("TwoFingerPointer",surface.TwoFingerPointer);
        surface.EdgeSliders=EdgeSwitch.IsOn;
        if(!surface.EdgeSliders)EdgeStatus.Visibility=Visibility.Collapsed;
        AppPreferences.Set("EdgeSliders",surface.EdgeSliders);
        AppPreferences.Set("MediaGestures",MediaSwitch.IsOn);
        ApplyInputMode();
        UpdateReceiver();
    }
    void UpdateReceiver()
    {
        if(!surface.ModeEnabled && !MediaSwitch.IsOn && !surface.EdgeSliders)
        {
            touchInput?.Dispose();touchInput=null;
            swipeGuard?.Dispose();swipeGuard=null;
            deviceReady=false;
            return;
        }
        if(touchInput!=null)return;
        try{
            touchInput=new TouchInput(inputWindow,ReceiveFrame,() =>
            {
                deviceReady=false;
                surface.Positioning=false;
                surface.Keyboard=false;
                ResetInput();
                ApplyInputMode();
            });
            swipeGuard=SwipeGuard.Open();
            surface.GuardFourFingerSwipe=active=>swipeGuard?.Set(active);
            GuardInfo.Text=swipeGuard==null?"Windows manages four-finger swipes on this PC.":
                "Four-finger swipe protection works while this window is in front. Windows handles four-finger swipes in other apps.";
        }catch(InvalidOperationException ex){MediaFeedback.Text=ex.Message;ShowKeyboardError(ex.Message);}
    }
    void ReceiveFrame(byte[] data,int offset)
    {
        bool keys=surface.ModeEnabled && !surface.Positioning;
        bool gestures=MediaSwitch.IsOn && !surface.Positioning;
        if(!deviceReady)
        {
            deviceReady=true;
            ApplyInputMode();
        }
        var old=surface.Hover;
        var oldModifier=surface.ModifierContact;
        surface.Frame(data,offset,inputClock.ElapsedMilliseconds,keys,gestures);

        overlay?.SetGestureDimmed(surface.PointingOrScrolling);
        overlay?.SetTouching(surface.HasContacts);
        if(old==surface.Hover && oldModifier==surface.ModifierContact)return;
        overlay?.Highlight(surface.Hover,surface.ModifierContact);
        if(old!=null)keyViews[old].Background=null;
        if(oldModifier!=null)keyViews[oldModifier].Background=null;
        var accent=(Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"];
        if(surface.Hover!=null)keyViews[surface.Hover].Background=accent;
        if(surface.ModifierContact!=null)keyViews[surface.ModifierContact].Background=accent;
    }
    void ShowKeyboardError(string message)
    {
        KeyboardError.Message=message;
        KeyboardError.IsOpen=true;
    }

    void EmitInput(string name)
    {
        bool media=name is "Play / pause" or "Previous track" or "Next track" or "Volume up" or "Volume down";
        if(!media && KeyboardSelected && GetForegroundWindow()==inputWindow)Practice.Focus(FocusState.Programmatic);
        string result=keyOutput.Send(name,StickerKey.ModifierCode(surface.ModifierContact?.Name));
        if(media)MediaFeedback.Text=result;
        else if(result is "Windows declined key input" or "Unknown key")ShowKeyboardError(result);
    }
}
