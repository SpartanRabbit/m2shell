@echo off
setlocal
title Building DiscordHello
echo Building DiscordHello.exe ...
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Could not find the .NET Framework compiler on this PC.
ping 127.0.0.1 -n 1 -w 200 >nul
exit /b 1
)
set "SRC=%TEMP%\DiscordHello_src.cs"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$l=Get-Content -LiteralPath '%~f0'; $i=[array]::IndexOf($l,'::CSHARP_BELOW'); $l[($i+1)..($l.Count-1)] | Set-Content -LiteralPath '%SRC%' -Encoding UTF8"
"%CSC%" /nologo /target:winexe /out:"%~dp0DiscordHello.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%SRC%"
del "%SRC%" >nul 2>&1
if exist "%~dp0DiscordHello.exe" (
    powershell -NoProfile -Command "Unblock-File -LiteralPath '%~dp0DiscordHello.exe'" >nul 2>&1
    echo.
    echo Done! DiscordHello.exe was created next to this file.
) else (
    echo.
    echo Build failed. Check the messages above.
)
ping 127.0.0.1 -n 1 -w 200 >nul
exit /b
::CSHARP_BELOW
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class DiscordHelloForm : Form
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    // ---- Locked settings ----
    const string TARGET  = "viuww";
    const string MESSAGE = "viuwwisaura";

    // ---- Speed settings (milliseconds). Lower = faster. Raise if it misfires. ----
    const int AUTO_DELAY     = 100;  // wait after the program opens, before auto-sending
    const int LOAD_WAIT      = 2000; // only used when Discord had to be started
    const int FOCUS_WAIT     = 100;  // after bringing Discord to the front
    const int SWITCHER_WAIT  = 100;  // after opening Ctrl+K
    const int SEARCH_WAIT    = 250;  // after pasting the name, before Enter
    const int CHANNEL_WAIT   = 200;  // after opening the chat, before pasting
    const int PASTE_WAIT     = 30;   // tiny pause around each paste

    TextBox txtTarget, txtMessage;
    Button btnSend;
    Label lblStatus, lblCredit;

    public DiscordHelloForm()
    {
        Text = "Discord Hello";
        ClientSize = new Size(380, 235);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        Label l0 = new Label();
        l0.Text = "To:";
        l0.Location = new Point(15, 15);
        l0.AutoSize = true;
        Controls.Add(l0);

        txtTarget = new TextBox();
        txtTarget.Text = TARGET;
        txtTarget.ReadOnly = true;
        txtTarget.TabStop = false;
        txtTarget.Location = new Point(15, 38);
        txtTarget.Width = 350;
        Controls.Add(txtTarget);

        Label l1 = new Label();
        l1.Text = "Message:";
        l1.Location = new Point(15, 72);
        l1.AutoSize = true;
        Controls.Add(l1);

        txtMessage = new TextBox();
        txtMessage.Text = MESSAGE;
        txtMessage.ReadOnly = true;
        txtMessage.TabStop = false;
        txtMessage.Location = new Point(15, 95);
        txtMessage.Width = 350;
        Controls.Add(txtMessage);

        btnSend = new Button();
        btnSend.Text = "Send again";
        btnSend.Location = new Point(15, 135);
        btnSend.Size = new Size(350, 36);
        btnSend.Click += delegate { Run(); };
        Controls.Add(btnSend);

        lblStatus = new Label();
        lblStatus.Text = "Starting...";
        lblStatus.Location = new Point(15, 182);
        lblStatus.AutoSize = true;
        lblStatus.ForeColor = Color.DimGray;
        Controls.Add(lblStatus);

        lblCredit = new Label();
        lblCredit.Text = "Made by viuww";
        lblCredit.AutoSize = false;
        lblCredit.TextAlign = ContentAlignment.MiddleCenter;
        lblCredit.Location = new Point(0, 208);
        lblCredit.Size = new Size(380, 22);
        lblCredit.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        lblCredit.ForeColor = Color.FromArgb(88, 101, 242); // Discord blurple
        Controls.Add(lblCredit);

        Shown += delegate { AutoStart(); };
    }

    void AutoStart()
    {
        Wait(AUTO_DELAY);
        Run();
        if (lblStatus.Text == "Done!") { Wait(500); Close(); }
    }

    void Status(string s) { lblStatus.Text = s; Application.DoEvents(); }

    void Wait(int ms)
    {
        DateTime end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
    }

    Process FindDiscord()
    {
        foreach (Process p in Process.GetProcessesByName("Discord"))
        {
            if (p.MainWindowHandle != IntPtr.Zero) return p;
        }
        return null;
    }

    void Launch()
    {
        string update = System.IO.Path.Combine(
            Environment.GetEnvironmentVariable("LocalAppData"), "Discord", "Update.exe");
        try
        {
            if (System.IO.File.Exists(update))
                Process.Start(update, "--processStart Discord.exe");
            else
                Process.Start(new ProcessStartInfo("discord://") { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("discord://") { UseShellExecute = true });
        }
    }

    void Focus(IntPtr h)
    {
        ShowWindow(h, 9); // restore if minimized
        keybd_event(0x12, 0, 0, UIntPtr.Zero);   // Alt down (lets Windows allow the focus change)
        keybd_event(0x12, 0, 2, UIntPtr.Zero);   // Alt up
        SetForegroundWindow(h);
    }

    // Puts text on the clipboard and pastes it instantly
    void Paste(string text)
    {
        Clipboard.SetDataObject(text, true, 10, 50);
        Wait(PASTE_WAIT);
        SendKeys.SendWait("^v");
        Wait(PASTE_WAIT);
    }

    void Run()
    {
        btnSend.Enabled = false;
        try
        {
            Process p = FindDiscord();
            if (p == null)
            {
                Status("Starting Discord...");
                Launch();
                for (int i = 0; i < 240 && p == null; i++)
                {
                    Wait(250);
                    p = FindDiscord();
                }
                if (p == null) { Status("Couldn't find the Discord window."); return; }
                Status("Waiting for Discord to load...");
                Wait(LOAD_WAIT);
            }

            Focus(p.MainWindowHandle);
            Wait(FOCUS_WAIT);

            Status("Sending to " + TARGET + "...");
            SendKeys.SendWait("^k");
            Wait(SWITCHER_WAIT);
            Paste(TARGET);
            Wait(SEARCH_WAIT);
            SendKeys.SendWait("{ENTER}");
            Wait(CHANNEL_WAIT);
            Paste(MESSAGE);
            SendKeys.SendWait("{ENTER}");
            Status("Done!");
        }
        catch (Exception ex)
        {
            Status("Error: " + ex.Message);
        }
        finally
        {
            btnSend.Enabled = true;
        }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new DiscordHelloForm());
    }
}
