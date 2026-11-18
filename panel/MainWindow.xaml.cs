// Adapted for Magic Trackpad Setup for Windows, 2026-11-17. GPLv2.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AmtPtpControlPanel.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        private bool _updatingLanguageCombo;

        private bool showUpdateSummary;
        [System.Runtime.InteropServices.DllImport("user32")]
        private static extern bool SetForegroundWindow(nint hwnd);

        public MainWindow(bool updated=false)
        {
            showUpdateSummary=updated;
            InitializeComponent();
            Closed += (_, _) => { updateLifetime.Cancel(); DiscardUpdate(); };

            SystemBackdrop = new MicaBackdrop();
            Title = LocalizationManager.GetString("Main", "Magic Trackpad");

            // Extend the Mica backdrop into the title bar and make the header
            // the draggable title area so the caption bar matches the body.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(HeaderPanel);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindowTitleBar titleBar = AppWindow.TitleBar;
                titleBar.BackgroundColor = Colors.Transparent;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            string iconPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "MagicTrackpad.ico"));
            if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
            InitializeInput();
            RootGrid.Loaded += MainWindow_Loaded;
            RootGrid.ActualThemeChanged += (s, e) => UpdateTitleBarButtonColors();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarButtonColors();
            ApplyLocalization();
            PopulateLanguageCombo();
            LoadStartupPreference();
            BatteryAlert.IsChecked = AppPreferences.Enabled(AppPreferences.LowBatteryAlert, true);
            ShowTrackpadSettings(SettingsService.Load(), AppPreferences.Enabled(AppPreferences.IndependentRelease, false));

            if(showUpdateSummary)
            {
                showUpdateSummary=false;
                AppSection.IsExpanded=true;
                AppWindow.Show();
                AppWindow.MoveInZOrderAtTop();
                Activate();
                SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
                string version=AppUpdates.Current.ToString();
                if(AppPreferences.ReadText("LastUpdateSummary","")!=version)
                {
                    AppPreferences.Set("LastUpdateSummary",version);
                    var notes=T("Update.Notes."+version,T("Update.Complete","Your update is ready."));
                    await ShowMessageAsync("v"+version,
                        notes.Split('\n',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                        (DataTemplate)RootGrid.Resources["ReleaseNotesTemplate"]);
                }
            }
            await UpdateBatteryAsync(showErrorOnFailure: false);
        }

        private void Section_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled ||
                sender.Content is not FrameworkElement body) return;
            var transform = new TranslateTransform();
            body.RenderTransform = transform;
            var movement = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation {
                From = -6, To = 0, Duration = TimeSpan.FromMilliseconds(155),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }, EnableDependentAnimation = true
            };
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(movement, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(movement, "Y");
            storyboard.Children.Add(movement);
            storyboard.Begin();
        }

        private bool syncingStartup = true;
        private void LoadStartupPreference()
        {
            syncingStartup = true;
            try { StartupSwitch.IsOn = StartupPreference.Enabled(); }
            catch { StartupSwitch.IsEnabled = false; StartupHint.Text = T("Startup.ReadError", "Windows startup settings are unavailable."); }
            finally { syncingStartup = false; }
        }
        private async void Startup_Toggled(object sender, RoutedEventArgs args)
        {
            if (syncingStartup) return;
            try
            {
                if (StartupSwitch.IsOn)
                    StartupPreference.Enable(System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value, false);
                else StartupPreference.Disable();
            }
            catch (Exception ex)
            {
                LoadStartupPreference();
                await ShowMessageAsync(T("Startup.Title", "Open at startup"), ex.Message);
            }
        }
        private async void BatteryAlert_Click(object sender, RoutedEventArgs e)
        {
            try { AppPreferences.Set(AppPreferences.LowBatteryAlert, BatteryAlert.IsChecked == true); }
            catch (Exception ex)
            {
                BatteryAlert.IsChecked = BatteryAlert.IsChecked != true;
                await ShowMessageAsync(Title, ex.Message);
            }
        }

        private void PopulateLanguageCombo()
        {
            _updatingLanguageCombo = true;

            LanguageComboBox.Items.Clear();
            foreach (string code in LocalizationManager.AvailableLanguages)
            {
                LanguageComboBox.Items.Add(new ComboBoxItem
                {
                    Content = GetLanguageDisplayName(code),
                    Tag = code
                });
            }

            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (string.Equals(item.Tag as string, LocalizationManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            _updatingLanguageCombo = false;
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingLanguageCombo)
                return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string code)
            {
                LocalizationManager.SetLanguage(code);
                ApplyLocalization();
                await UpdateBatteryAsync(showErrorOnFailure: false);
            }
        }

        private static string GetLanguageDisplayName(string code)
        {
            return code switch
            {
                "ar" => "العربية",
                "it" => "Italiano",
                "es" => "Español",
                "fr" => "Français",
                "de" => "Deutsch",
                "pt" => "Português",
                "ru" => "Русский",
                "zh" => "中文",
                "ja" => "日本語",
                "ko" => "한국어",
                _ => "English"
            };
        }

        private void UpdateTitleBarButtonColors()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
                return;

            AppWindowTitleBar titleBar = AppWindow.TitleBar;
            bool dark = RootGrid.ActualTheme == ElementTheme.Dark;
            Windows.UI.Color foreground = dark ? Colors.White : Colors.Black;

            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = Colors.Gray;
        }

        private static string T(string key, string fallback) =>
            LocalizationManager.GetString(key, fallback);

        private void ApplyLocalization()
        {
            var language = LocalizationManager.CurrentLanguage;
            RootGrid.Language = string.IsNullOrEmpty(language) ? "en" : language;
            RootGrid.FlowDirection = language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Title = T("Main", "Magic Trackpad");
            TitleText.Text = Title;
            SettingsTab.Text = T("Input.SettingsTab", "Trackpad");
            KeyboardTab.Text = T("Input.KeyboardTab", "Keyboard");
            GestureTitle.Text = T("Input.GestureTitle", "Extra gestures");
            EdgeSwitch.Header = T("Input.EdgeSwitch", "Edge sliders");
            EdgeHelp.Text = T("Input.EdgeHelp", "In trackpad mode, slide one finger along the left edge for screen brightness or the right edge for volume. The bottom is 0% and the top is 100%.");
            MediaSwitch.Header = T("Input.MediaSwitch", "Five-finger gestures");
            WindowsGestureLink.Content = T("Input.WindowsGestureLink", "Three- and four-finger settings in Windows");
            KeyboardSwitch.Header = T("Input.KeyboardSwitch", "Sticker keyboard");
            ModeTitle.Text = T("Input.ModeTitle", "Switch modes.") + " ";
            ModeHelp.Text = T("Input.ModeHelp", "Click the top-right corner to switch between typing and pointing.");
            PointerTitle.Text = T("Input.PointerTitle", "Point and scroll.") + " ";
            PointerHelp.Text = T("Input.PointerHelp", "With two-finger pointing enabled, move two fingers to use the pointer while typing. Keep them down, add a third finger, then slide up or down to scroll. Lift all fingers to resume typing.");
            MoveTitle.Text = T("Input.MoveTitle", "Move the overlay.") + " ";
            MoveHelp.Text = T("Input.MoveHelp", "Hold a click in that corner, then drag the handle. Click or hold the corner again to resume typing.");
            ShortcutTitle.Text = T("Input.ShortcutTitle", "Use shortcuts.") + " ";
            ShortcutHelp.Text = T("Input.ShortcutHelp", "Rest one finger on Shift, Ctrl, Alt, or Win and press another key.");
            RestorePointerTitle.Text = T("Input.RestorePointerTitle", "Return to pointing.") + " ";
            RestorePointerHelp.Text = T("Input.RestorePointerHelp", "Press Ctrl+Alt+F10 on your physical keyboard.");
            TapSwitch.Content = T("Input.TapSwitch", "Tap to type");
            CursorGesture.Content = T("Input.CursorGesture", "Use two fingers to point, click, and drag in keyboard mode");
            OverlayButton.Content = T("Input.OverlayButton", "Floating keyboard");
            PositionButton.Content = T("Input.PositionButton", "Position overlay");
            OverlayTransparency.Header = T("Input.OverlayTransparency", "Transparency");
            TransparencyHelp.Text = T("Input.TransparencyHelp", "Higher makes the keyboard more see-through.");
            FadeIdle.Header = T("Input.FadeIdle", "Fade when idle");
            IdleAmount.Header = T("Input.IdleAmount", "Idle dimming");
            IdleHelp.Text = T("Input.IdleHelp", "Higher makes the keyboard more see-through when you're not using it");
            IdleDuration.Header = T("Input.IdleDuration", "Fade duration");
            DurationHelp.Text = T("Input.DurationHelp", "Higher makes idle dimming take more time to work.");
            OverlayScale.Header = T("Input.OverlayScale", "Scale");
            ScaleHelp.Text = T("Input.ScaleHelp", "Higher makes the keyboard larger.");
            Practice.PlaceholderText = T("Input.Practice", "Try typing here");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(GesturesSection, GestureTitle.Text);
            InitializeGestureRows();
            UpdateGestureHint();
            UpdateAppearanceLabels();
            MediaFeedback.Text = "";
            EdgeStatus.Visibility = Visibility.Collapsed;

            ClickFeedbackTitle.Text = T("Section.ClickFeedbackMacOS", "Click feel");
            MacOSClickOptions.Content = T("ctlMacOSClickOptions", "Adjust click strength");
            SilentClicking.Content = T("ctlSilentClicking", "Use a quieter click");
            ClickStrengthLabel.Text = T("Section.ClickStrength", "Click strength");
            LightLabel.Text = T("ctlLightLabel", "Light");
            MediumLabel.Text = T("ctlMediumLabel", "Medium");
            FirmLabel.Text = T("ctlFirmLabel", "Firm");

            ReleaseStrength.Header = T("Click.Release", "Release feedback");
            ReleaseMatch.Content = T("Click.Match", "Match press strength");
            ReleaseLight.Content = LightLabel.Text;
            ReleaseMedium.Content = MediumLabel.Text;
            ReleaseFirm.Content = FirmLabel.Text;
            BatteryAlert.Content = T("Battery.Alert", "Notify me at 20% battery");
            DisableFeedback.Content = T("ctlDisableFeedback", "Turn off the physical click and its feedback");
            MaximumFeedback.Content = T("ctlMaximumFeedback", "Use the strongest click feedback");

            LiftFingerTitle.Text = T("groupBox3", "Pointer lift-off");
            StopDoNothing.Content = T("ctlStopDoNothing", "Keep tracking until I lift my finger");
            StopPressureText.Text = T("ctlStopPressure", "Stop tracking when pressure falls to this level");
            StopPressureLabelText.Text = T("ctlStopPressureLabel", "Suggested: 0");
            StopSizeText.Text = T("ctlStopSize", "Stop tracking when the contact area shrinks to this size");
            StopSizeLabelText.Text = T("ctlStopSizeLabel", "Suggested: 7");

            OtherOptionsTitle.Text = T("groupBox4", "Touch and pointer settings");
            IgnoreNearFingers.Content = T("ctlIgnoreNearFingers", "Ignore fingers hovering above the surface");
            IgnoreButtonFingerText.Text = T("ctlIgnoreButtonFinger", "Ignore the finger holding a click while I drag");
            PalmRejection.Content = T("ctlPalmRejection", "Ignore accidental palm touches");
            InvertMovementText.Text = T("ctlInvertMovement", "Reverse both pointer directions for a rotated trackpad");
            AllowNonAdminAccessText.Text = T("ctlAllowNonAdminAccess", "Allow other apps to control haptics without administrator access");
            AppSettingsTitle.Text = T("App.Title", "App settings");
            StartupSwitch.Header = T("Startup.Title", "Open at startup");
            foreach(var toggle in new[]{StartupSwitch,EdgeSwitch,MediaSwitch,KeyboardSwitch,FadeIdle})
            {
                if (language == "ar")
                {
                    toggle.OnContent = T("Startup.On", "On");
                    toggle.OffContent = T("Startup.Off", "Off");
                }
                else
                {
                    toggle.ClearValue(ToggleSwitch.OnContentProperty);
                    toggle.ClearValue(ToggleSwitch.OffContentProperty);
                }
            }
            StartupHint.Text = T("Startup.Hint", "Show the battery beside the clock when you sign in. The settings window opens when you need it.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AppSection, AppSettingsTitle.Text);
            UpdateLabels();
            LanguageLabel.Text = T("ctlLanguage", "Language");

            BatteryTitle.Text = T("Section.Battery", "Battery");
            UpdateBatteryButton.Content = T("ctlBatteryUpdate", "Refresh");

            TouchpadSettingsButton.Content = T("ctlTouchpadSettings", "Windows gestures");
            ApplyButton.Content = T("ctlApply", "Save changes");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ClickSection, ClickFeedbackTitle.Text);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LiftSection, LiftFingerTitle.Text);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TouchSection, OtherOptionsTitle.Text);
            CopyDetailsButton.Content = T("Details.Copy", "Copy device details");
            DefaultsButton.Content = T("Defaults.Restore", "Restore trackpad defaults");
            DefaultsHint.Text = T("Defaults.Hint", "Review the defaults, then select Save changes.");
            GitHubLink.Content = T("Link.GitHub", "Help");
        }

        private void Footer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool narrow = e.NewSize.Width < 660;
            Grid.SetColumnSpan(HelpActions, narrow ? 3 : 1);
            Grid.SetRow(TouchpadSettingsButton, narrow ? 1 : 0);
            Grid.SetRow(ApplyButton, narrow ? 1 : 0);
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            ShowTrackpadSettings(new TrackpadSettings(), false);
            DefaultsHint.Visibility = Visibility.Visible;
            DefaultsHint.StartBringIntoView();
        }

        private async void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            CopyDetailsButton.IsEnabled = false;
            try
            {
                var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                data.SetText(await Task.Run(DeviceDetails.Read));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                CopyDetailsButton.Content = "✓ " + T("Details.Copy", "Copy device details");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(T("Message.Error", "Magic Trackpad"), ex.Message);
            }
            finally
            {
                CopyDetailsButton.Content = T("Details.Copy", "Copy device details");
                CopyDetailsButton.IsEnabled = true;
            }
        }

        private void ShowTrackpadSettings(TrackpadSettings s, bool independentRelease)
        {

            if (s.ButtonDisabled != 0)
            {
                DisableFeedback.IsChecked = true;
            }
            else if (s.FeedbackClick == 0xffffff && s.FeedbackRelease == 0xffffff)
            {
                MaximumFeedback.IsChecked = true;
            }
            else
            {
                MacOSClickOptions.IsChecked = true;

                SilentClicking.IsChecked = (s.FeedbackClick & 0xffff00) == 0 && (s.FeedbackRelease & 0xffff00) == 0;

                int press = StrengthIndex(PressValues, s.FeedbackClick);
                int release = StrengthIndex(ReleaseValues, s.FeedbackRelease);
                FeedbackSlider.Value = press;
                ReleaseStrength.SelectedIndex = release != press || independentRelease
                    ? release + 1 : 0;
                initialPresets = PresetFeedback();
                initialFeedback = (s.FeedbackClick, s.FeedbackRelease);
            }

            StopPressureValue.Value = s.StopPressure < 0 ? 0 : s.StopPressure;
            StopSizeValue.Value = s.StopSize < 0 ? 7 : s.StopSize;
            if (s.StopPressure == -1 && s.StopSize == -1)
            {
                StopDoNothing.IsChecked = true;
            }
            else if (s.StopPressure != -1)
            {
                StopPressure.IsChecked = true;
            }
            else
            {
                StopSize.IsChecked = true;
            }

            IgnoreButtonFinger.IsChecked = s.IgnoreButtonFinger != 0;
            IgnoreNearFingers.IsChecked = s.IgnoreNearFingers != 0;
            PalmRejection.IsChecked = s.PalmRejection != 0;
            InvertMovement.IsChecked = s.InvertMovement != 0;
            AllowNonAdminAccess.IsChecked = s.AllowNonAdminAccess != 0;
        }

        private void ClickOptions_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radio || radio.IsChecked != true)
                return;

            if (ReferenceEquals(radio, MacOSClickOptions))
            {
                MaximumFeedback.IsChecked = false;
                DisableFeedback.IsChecked = false;
            }
            else if (ReferenceEquals(radio, MaximumFeedback))
            {
                MacOSClickOptions.IsChecked = false;
                DisableFeedback.IsChecked = false;
            }
            else if (ReferenceEquals(radio, DisableFeedback))
            {
                MacOSClickOptions.IsChecked = false;
                MaximumFeedback.IsChecked = false;
            }

            bool macEnabled = MacOSClickOptions.IsChecked == true;
            SilentClicking.IsEnabled = macEnabled;
            FeedbackSlider.IsEnabled = macEnabled;
            ReleaseStrength.IsEnabled = macEnabled;
        }

        private void StopOptions_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radio || radio.IsChecked != true)
                return;

            if (ReferenceEquals(radio, StopDoNothing))
            {
                StopPressure.IsChecked = false;
                StopSize.IsChecked = false;
            }
            else if (ReferenceEquals(radio, StopPressure))
            {
                StopDoNothing.IsChecked = false;
                StopSize.IsChecked = false;
            }
            else if (ReferenceEquals(radio, StopSize))
            {
                StopDoNothing.IsChecked = false;
                StopPressure.IsChecked = false;
            }

            StopPressureValue.IsEnabled = StopPressure.IsChecked == true;
            StopSizeValue.IsEnabled = StopSize.IsChecked == true;
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = false;
            try
            {
                if (await SaveSettingsAsync())
                {
                    DefaultsHint.Visibility = Visibility.Collapsed;
                    await Task.Run(TrackpadDevice.ReloadSettings);

                    ApplyButton.Content = "✓ " + T("ctlApply", "Save changes");
                    await Task.Delay(900);
                    ApplyButton.Content = T("ctlApply", "Save changes");
                }
            }
            finally
            {
                ApplyButton.IsEnabled = true;
            }
        }

        private void TouchpadSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:devices-touchpad")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // Opening Windows Settings is best-effort only.
            }
        }

        private async void UpdateBatteryButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateBatteryAsync(showErrorOnFailure: true);
        }

        private async Task UpdateBatteryAsync(bool showErrorOnFailure)
        {
            if (!UpdateBatteryButton.IsEnabled) return;
            UpdateBatteryButton.IsEnabled = false;
            try
            {
                var reading = await Task.Run(() =>
                {
                    string connection = TrackpadDevice.Read(out int percent);
                    return (source: connection, level: percent);
                });
                if (reading.level >= 0)
                {
                    ShowBattery(reading.source, (uint)reading.level);
                    return;
                }
                BatteryProgressBar.Value = 0;
                BatteryTitle.Text = T("Section.Battery", "Battery");
                BatteryStatusText.Text = T("Message.NoDevice", "Connect your trackpad with a USB cable or pair it in Windows Bluetooth settings, then refresh.");
                if (showErrorOnFailure) await ShowMessageAsync(T("Main", "Magic Trackpad"), BatteryStatusText.Text);
            }
            finally { UpdateBatteryButton.IsEnabled = true; }
        }
        private void ShowBattery(string source, uint level)
        {
            if (level > 100)
                return;

            BatteryProgressBar.Value = level;

            int bucket = Math.Clamp((int)Math.Round(level / 10.0, MidpointRounding.AwayFromZero), 0, 10);
            int glyphCode = source == "USB" ? 0xF5FD + bucket : 0xF5F2 + bucket;
            BatteryIcon.Glyph = ((char)glyphCode).ToString();

            BatteryTitle.Text = string.Format("{0} ({1}%)", T("Section.Battery", "Battery"), level);
            BatteryStatusText.Text = string.Format(
                T("Battery.LastUpdated", "{0}  ·  Updated {1}"),
                source == "Bluetooth" ? T("Battery.Bluetooth", source) : source,
                DateTime.Now.ToString("t", CultureInfo.CurrentCulture));
        }

        static readonly int[] PressValues = { 0x040415, 0x060617, 0x08081e };
        static readonly int[] ReleaseValues = { 0x000010, 0x000014, 0x020218 };
        (int, int)? initialPresets;
        (int, int) initialFeedback;

        static int StrengthIndex(int[] presets, int value)
        {
            int index = Array.FindIndex(presets, preset => (preset & 255) == (value & 255));
            return index < 0 ? 1 : index;
        }

        (int press, int release) SelectedFeedback()
        {
            if (DisableFeedback.IsChecked == true) return (0, 0);
            if (MaximumFeedback.IsChecked == true) return (0xffffff, 0xffffff);
            var selected = PresetFeedback();
            // Preserve raw driver tuning when saving unrelated settings.
            return selected == initialPresets ? initialFeedback : selected;
        }

        (int press, int release) PresetFeedback()
        {
            int press = (int)FeedbackSlider.Value;
            int release = ReleaseStrength.SelectedIndex <= 0 ? press : ReleaseStrength.SelectedIndex - 1;
            int mask = SilentClicking.IsChecked == true ? 0xff : 0xffffff;
            return (PressValues[press] & mask, ReleaseValues[release] & mask);
        }

        private async Task<bool> SaveSettingsAsync()
        {
            TrackpadSettings s = new TrackpadSettings();

            (s.FeedbackClick, s.FeedbackRelease) = SelectedFeedback();
            s.ButtonDisabled = DisableFeedback.IsChecked == true ? 1 : 0;

            if (StopDoNothing.IsChecked == true)
            {
                s.StopPressure = -1;
                s.StopSize = -1;
            }
            else if (StopPressure.IsChecked == true)
            {
                s.StopSize = -1;

                if (!TryReadInt(StopPressureValue, out s.StopPressure) || s.StopPressure < 0)
                {
                    await ShowMessageAsync(
                        T("Message.Error", "Magic Trackpad"),
                        T("Message.PressureInvalid", "Enter a pressure of 0 or more."));
                    return false;
                }
            }
            else
            {
                s.StopPressure = -1;

                if (!TryReadInt(StopSizeValue, out s.StopSize) || s.StopSize < 0)
                {
                    await ShowMessageAsync(
                        T("Message.Error", "Magic Trackpad"),
                        T("Message.SizeInvalid", "Enter a contact size of 0 or more."));
                    return false;
                }
            }

            s.IgnoreButtonFinger = IgnoreButtonFinger.IsChecked == true ? 1 : 0;
            s.IgnoreNearFingers = IgnoreNearFingers.IsChecked == true ? 1 : 0;
            s.PalmRejection = PalmRejection.IsChecked == true ? 1 : 0;
            s.InvertMovement = InvertMovement.IsChecked == true ? 1 : 0;
            s.AllowNonAdminAccess = AllowNonAdminAccess.IsChecked == true ? 1 : 0;

            try
            {
                SettingsService.Save(s);
                if (MacOSClickOptions.IsChecked == true)
                    AppPreferences.Set(AppPreferences.IndependentRelease, ReleaseStrength.SelectedIndex > 0);
                return true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(
                    T("Message.Error", "Magic Trackpad"),
                    string.Format(T("Message.RegistryWriteError", "Your changes could not be saved. {0}"), ex.Message));
                return false;
            }
        }

        private static bool TryReadInt(NumberBox box, out int value)
        {
            value = 0;

            double v = box.Value;
            if (double.IsNaN(v))
                return false;

            value = (int)Math.Round(v);
            return true;
        }

        private async Task ShowMessageAsync(string title, object message, DataTemplate? template=null)
        {
            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = title,
                Content = message,
                ContentTemplate = template,
                FlowDirection = RootGrid.FlowDirection,
                CloseButtonText = T("Dialog.OK", "OK"),
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }
    }
}
