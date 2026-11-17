using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AmtPtpControlPanel.WinUI3;

public sealed partial class MainWindow
{
    readonly CancellationTokenSource updateLifetime = new();
    AppRelease? pendingRelease;
    string? pendingInstaller;
    bool checkingUpdate;
    string updateMessageKey = "";

    void UpdateLabels()
    {
        UpdateAppButton.Content = pendingInstaller == null ? T("Update.Check", "Check for updates") : T("Update.Restart", "Restart and update");
        UpdateStatus.Text = updateMessageKey switch
        {
            "Update.Latest" => T(updateMessageKey, "You’re up to date."),
            "Update.Checking" => T(updateMessageKey, "Checking for updates…"),
            "Update.Download" => T(updateMessageKey, "Downloading the update…"),
            "Update.Ready" => string.Format(T(updateMessageKey, "Version {0} is ready to install."), pendingRelease?.Version),
            "Update.Failed" => T(updateMessageKey, "Couldn’t complete the update. Try again."),
            _ => string.Format(T("Update.Version", "Version {0}"), AppUpdates.Current)
        };
    }

    async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (checkingUpdate) return;
        checkingUpdate = true;
        UpdateAppButton.IsEnabled = false;
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        try
        {
            if (pendingInstaller == null)
            {
                updateMessageKey = "Update.Checking";
                UpdateLabels();
                pendingRelease = await AppUpdates.Check(updateLifetime.Token);
                if (pendingRelease == null) { updateMessageKey = "Update.Latest"; return; }
                updateMessageKey = "Update.Download";
                UpdateLabels();
                pendingInstaller = await AppUpdates.Download(pendingRelease, Path.Combine(root, "Updates"), updateLifetime.Token);
            }
            if (updateLifetime.IsCancellationRequested) return;
            updateMessageKey = "Update.Ready";
            UpdateLabels();
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot, FlowDirection = RootGrid.FlowDirection, Title = UpdateStatus.Text,
                Content = T("Update.Prompt", "Save any pending settings before restarting Magic Trackpad."),
                PrimaryButtonText = T("Update.Restart", "Restart and update"), CloseButtonText = T("Update.Later", "Later"),
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AppUpdates.Restart(root, pendingInstaller, pendingRelease!.Digest);
                pendingInstaller = null;
                Close();
            }
        }
        catch (Exception) { updateMessageKey = "Update.Failed"; }
        finally
        {
            if (updateLifetime.IsCancellationRequested) DiscardUpdate();
            else { UpdateLabels(); UpdateAppButton.IsEnabled = true; }
            checkingUpdate = false;
        }
    }

    void DiscardUpdate()
    {
        if (pendingInstaller != null) { AppUpdates.Discard(pendingInstaller); pendingInstaller = null; }
    }
}
