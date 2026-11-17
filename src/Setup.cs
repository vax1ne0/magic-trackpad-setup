using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class Setup : Form
{
    const string archive = "MT2FW11-amd64-20261111-MSSigned.zip";
    const string hash = "CBCC67302E03B83733A3C4E42B1667E6AE3BFE333B33879CDE2F4115A6608121";
    static readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string controlPanel = Path.Combine(root,"TrackpadControlPanel.exe");
    readonly Label status = new Label();
    readonly Button install = new Button(), panel = new Button();
    bool busy;

    internal static void Prepare(string zip, string stage)
    {
        using(var file=File.OpenRead(zip))
        using(var sha=SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!=hash)
                throw new IOException("Package checksum mismatch. Download the official archive again.");
        using(var package=ZipFile.OpenRead(zip))
        {
            string[] names={"AMD64/AmtPtpDevice.inf","AMD64/amtptpdevice.cat","AMD64/AmtPtpDeviceUsbUm.dll","AMD64/AmtPtpHidFilter.sys","AmtPtpControlPanel.WinUI3-amd64.exe"};
            foreach(string name in names)
            {
                ZipArchiveEntry match=null;
                foreach(var entry in package.Entries)
                    if(string.Equals(entry.FullName.Replace('\\','/'),name,StringComparison.OrdinalIgnoreCase)) match=entry;
                if(match==null) throw new IOException("Missing release file "+name);
                match.ExtractToFile(Path.Combine(stage,Path.GetFileName(name)));
            }
        }
    }
    static void Deploy(string src,string dest)
    {
        if(File.Exists(dest))
        {
            using(var sha=SHA256.Create()) using(var a=File.OpenRead(src)) using(var b=File.OpenRead(dest))
                if(Convert.ToBase64String(sha.ComputeHash(a))==Convert.ToBase64String(sha.ComputeHash(b)))return;
        }
        File.Copy(src,dest,true);
    }
    static int Install(Action<string> progress)
    {
        using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            int build;
            if(!int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build)||build<22000 ||
                Convert.ToString(key.GetValue("InstallationType"))!="Client" ||
                !string.Equals(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE",EnvironmentVariableTarget.Machine),"AMD64",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This package requires Windows 11 on an Intel or AMD x64 PC.");
        }
        Directory.CreateDirectory(root);
        string stage=Directory.CreateDirectory(Path.Combine(root,"stage-"+Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string zip=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,archive);
            if(!File.Exists(zip))
            {
                progress("Downloading the official driver and control panel (60 MB)...");
                zip=Path.Combine(stage,archive);
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                using(var client=new WebClient()) client.DownloadFile("https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/download/v2.3/"+archive,zip);
            }
            progress("Checking the release checksum...");
            Prepare(zip,stage);
            progress("Installing the Microsoft-signed driver...");
            var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"pnputil.exe"),"/add-driver \""+Path.Combine(stage,"AmtPtpDevice.inf")+"\" /install");
            info.UseShellExecute=false;info.CreateNoWindow=true;
            int code;
            using(var p=Process.Start(info)){p.WaitForExit();code=p.ExitCode;}
            if(code!=0 && code!=3010)throw new IOException("Windows driver installation returned code "+code+".");
            string tray=Path.Combine(root,"TrackpadBattery.exe");
            Deploy(Path.Combine(stage,"AmtPtpControlPanel.WinUI3-amd64.exe"),controlPanel);



            Process.Start(tray);
            return code;
        }
        finally {Directory.Delete(stage,true);}
    }
    void LabelAt(string text,int y,int size,bool bold)
    {
        var label=new Label { Text=text,AutoSize=false,Location=new Point(25,y),Size=new Size(555,36),Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)};
        Controls.Add(label);
    }
    void ButtonAt(Button b,string title,int x,int width)
    {
        b.Text=title;b.SetBounds(x,290,width,38);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;
        b.BackColor=Color.FromArgb(52,62,78);Controls.Add(b);
    }
    internal Setup()
    {
        Text="Magic Trackpad Setup";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Segoe UI",10);ClientSize=new Size(605,351);
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(25,30,42);ForeColor=Color.WhiteSmoke;
        LabelAt("Magic Trackpad Setup",22,22,true);
        LabelAt("Magic Trackpad for Windows 11",70,10,false);
        LabelAt("Precision gestures and click settings",115,13,true);
        LabelAt("Includes the v2.3 driver and Windows 11 control panel.",148,10,false);
        LabelAt("Battery and remaining hours",185,13,true);
        status.Text="See battery charge in the taskbar. Runtime learns from Bluetooth use.";
        status.SetBounds(25,222,555,58);Controls.Add(status);
        ButtonAt(install,"Set up driver",25,140);install.BackColor=Color.FromArgb(104,207,249);install.ForeColor=Color.Black;
        var pair=new Button();ButtonAt(pair,"Bluetooth settings",177,160);pair.Click+=delegate{Process.Start("ms-settings:bluetooth");};
        ButtonAt(panel,"Open control panel",349,190);panel.Enabled=File.Exists(controlPanel);panel.Click+=delegate{Process.Start(controlPanel);};
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
        install.Click+=async delegate
        {
            busy=true;install.Enabled=false;
            try
            {
                int code=await Task.Run(()=>Install(s=>Invoke((Action)(()=>status.Text=s))));
                status.Text=code==3010?"Installed. Restart Windows to finish loading the driver.":"Installed. Open the control panel or pair the trackpad in Bluetooth settings.";
                panel.Enabled=true;install.Text="Check setup";
            }
            catch(Exception ex){status.Text="Setup could not finish. "+ex.Message;}
            finally{busy=false;install.Enabled=true;}
        };
    }
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Setup());}
}


