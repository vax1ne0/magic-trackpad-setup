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

        public MainWindow()
        {
            InitializeComponent();

            SystemBackdrop = new MicaBackdrop();
            Title = LocalizationManager.GetString("Main", "Magic Trackpad 2 Control Panel");

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
            RootGrid.Loaded += MainWindow_Loaded;
            RootGrid.ActualThemeChanged += (s, e) => UpdateTitleBarButtonColors();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarButtonColors();
            ApplyLocalization();
            PopulateLanguageCombo();
            LoadStartupPreference();
            LoadSettings();

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
                await ShowErrorAsync(T("Startup.Title", "Open at startup"), ex.Message);
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

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingLanguageCombo)
                return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string code)
            {
                LocalizationManager.SetLanguage(code);
                ApplyLocalization();
            }
        }

        private static string GetLanguageDisplayName(string code)
        {
            return code switch
            {
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
            Title = T("Main", "Magic Trackpad 2 Control Panel");
            TitleText.Text = Title;

            ClickFeedbackTitle.Text = T("Section.ClickFeedbackMacOS", "MacOS click options");
            MacOSClickOptions.Content = T("ctlMacOSClickOptions", "Use macOS Click Options:");
            SilentClicking.Content = T("ctlSilentClicking", "Silent clicking");
            ClickStrengthLabel.Text = T("Section.ClickStrength", "Click strength");
            LightLabel.Text = T("ctlLightLabel", "Light");
            MediumLabel.Text = T("ctlMediumLabel", "Medium");
            FirmLabel.Text = T("ctlFirmLabel", "Firm");

            DisableFeedback.Content = T("ctlDisableFeedback", "Disable haptic feedback and force touch button completely");
            MaximumFeedback.Content = T("ctlMaximumFeedback", "Maximum haptic feedback (very clicky and loud!)");

            LiftFingerTitle.Text = T("groupBox3", "When you lift your finger from the trackpad");
            StopDoNothing.Content = T("ctlStopDoNothing", "Do nothing");
            StopPressureText.Text = T("ctlStopPressure", "Stop the pointer if the pressure is less than or equal to");
            StopPressureLabelText.Text = T("ctlStopPressureLabel", "units. (0 means no pressure; 0 is a good value)");
            StopSizeText.Text = T("ctlStopSize", "Stop the pointer if the size of the touch area is less than or equal to");
            StopSizeLabelText.Text = T("ctlStopSizeLabel", "units. (7 is a good value)");

            OtherOptionsTitle.Text = T("groupBox4", "Other options");
            IgnoreNearFingers.Content = T("ctlIgnoreNearFingers", "Ignore input from fingers not touching the trackpad surface");
            IgnoreButtonFingerText.Text = T("ctlIgnoreButtonFinger", "Ignore input from the finger used to click the force touch button (useful for dragging, if you use your thumb to click the button and your index finger to move the pointer, for example)");
            PalmRejection.Content = T("ctlPalmRejection", "Palm Rejection");
            InvertMovementText.Text = T("ctlInvertMovement", "Invert up/down and left/right (for upside-down use)");
            AllowNonAdminAccessText.Text = T("ctlAllowNonAdminAccess", "ADVANCED: Let non-admin apps communicate with the drivers");
            AppSettingsTitle.Text = T("App.Title", "App settings");
            StartupSwitch.Header = T("Startup.Title", "Open at startup");
            StartupHint.Text = T("Startup.Hint", "Show the battery beside the clock when you sign in. The settings window opens when you need it.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AppSection, AppSettingsTitle.Text);
            LanguageLabel.Text = T("ctlLanguage", "UI language");

            BatteryTitle.Text = T("Section.Battery", "Battery");
            UpdateBatteryButton.Content = T("ctlBatteryUpdate", "Update Battery");

            TouchpadSettingsButton.Content = T("ctlTouchpadSettings", "Windows Touchpad Settings");
            ApplyButton.Content = T("ctlApply", "Apply");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ClickSection, ClickFeedbackTitle.Text);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LiftSection, LiftFingerTitle.Text);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TouchSection, OtherOptionsTitle.Text);
            GitHubLink.Content = T("Link.GitHub", "Project on GitHub");
        }

        private void LoadSettings()
        {
            TrackpadSettings s = SettingsService.Load();

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

                if ((s.FeedbackClick & 0xffff00) == 0 && (s.FeedbackRelease & 0xffff00) == 0)
                    SilentClicking.IsChecked = true;

                if ((s.FeedbackClick & 0x0000ff) == 0x15 && (s.FeedbackRelease & 0x0000ff) == 0x10)
                    FeedbackSlider.Value = 0;
                else if ((s.FeedbackClick & 0x0000ff) == 0x1e && (s.FeedbackRelease & 0x0000ff) == 0x18)
                    FeedbackSlider.Value = 2;
                else
                    FeedbackSlider.Value = 1;
            }

            if (s.StopPressure == -1 && s.StopSize == -1)
            {
                StopDoNothing.IsChecked = true;
            }
            else if (s.StopPressure != -1)
            {
                StopPressure.IsChecked = true;
                StopPressureValue.Value = s.StopPressure;
            }
            else
            {
                StopSize.IsChecked = true;
                StopSizeValue.Value = s.StopSize;
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
                BatteryStatusText.Text = T("Message.NoDevice", "Connect your trackpad, then refresh.");
                if (showErrorOnFailure) await ShowErrorAsync(T("Main", "Magic Trackpad"), BatteryStatusText.Text);
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
                T("Battery.LastUpdated", "Battery ({0}) --- LAST UPDATED: {1}"),
                source,
                DateTime.Now.ToString("t", CultureInfo.CurrentCulture));
        }

        private async Task<bool> SaveSettingsAsync()
        {
            TrackpadSettings s = new TrackpadSettings();

            if (DisableFeedback.IsChecked == true)
            {
                s.ButtonDisabled = 1;
                s.FeedbackClick = 0;
                s.FeedbackRelease = 0;
            }
            else if (MaximumFeedback.IsChecked == true)
            {
                s.ButtonDisabled = 0;
                s.FeedbackClick = 0xffffff;
                s.FeedbackRelease = 0xffffff;
            }
            else
            {
                s.ButtonDisabled = 0;

                int strength = (int)Math.Round(FeedbackSlider.Value);
                s.FeedbackClick = strength == 0 ? 0x040415 : strength == 1 ? 0x060617 : 0x08081e;
                s.FeedbackRelease = strength == 0 ? 0x000010 : strength == 1 ? 0x000014 : 0x020218;

                if (SilentClicking.IsChecked == true)
                {
                    s.FeedbackClick &= 0x0000ff;
                    s.FeedbackRelease &= 0x0000ff;
                }
            }

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
                    await ShowErrorAsync(
                        T("Message.Error", "Error"),
                        T("Message.PressureInvalid", "Pressure must be greater than or equal to 0."));
                    return false;
                }
            }
            else
            {
                s.StopPressure = -1;

                if (!TryReadInt(StopSizeValue, out s.StopSize) || s.StopSize < 0)
                {
                    await ShowErrorAsync(
                        T("Message.Error", "Error"),
                        T("Message.SizeInvalid", "Size must be greater than or equal to 0."));
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
                return true;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(
                    T("Message.Error", "Error"),
                    string.Format(T("Message.RegistryWriteError", "Error writing to the registry: {0}"), ex.Message));
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

        private async Task ShowErrorAsync(string title, string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }
    }
}
