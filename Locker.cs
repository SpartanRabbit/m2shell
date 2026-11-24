using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Timer = System.Windows.Forms.Timer;
using System.Windows.Forms;
using Microsoft.Win32;

class LockerForm : Form
{
    const int CountdownSeconds = 60;
    const int AutoUnlockSeconds = 1200; // 20 minutes
    const string PasswordHash = "2f9e50cf881d84da19d44dafea451c91c97aaab4cc7c5b080faae1491e60413e"; // SHA-256

    const string SysKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    const string ExpKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    static readonly string[] SysVals = { "DisableTaskMgr", "DisableLockWorkstation", "DisableChangePassword" };
    static readonly string[] ExpVals = { "NoLogoff", "NoClose" };

    bool allowClose = false, locked = false, entryVisible = true;
    int remaining = CountdownSeconds;
    int autoUnlockRemaining = AutoUnlockSeconds;
    HashSet<Keys> pressed = new HashSet<Keys>();

    Label title, count, hint, msg, autoLabel;
    TextBox box;
    Timer countdownTimer, keepTimer, autoUnlockTimer;

    public LockerForm()
    {
        Text = "Locker";
        FormBorderStyle = FormBorderStyle.None;
        ControlBox = false; MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false; TopMost = true; KeyPreview = true;
        BackColor = Color.Black;
        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Maximized;
        Bounds = Screen.PrimaryScreen.Bounds;

        title = MakeLabel("Locking this computer", 28, Color.FromArgb(220, 220, 220), FontStyle.Regular);
        count = MakeLabel("", 22, Color.Orange, FontStyle.Bold);
        hint = MakeLabel("Type the password and press Enter to cancel", 13, Color.Gray, FontStyle.Regular);
        msg = MakeLabel("Wrong password", 13, Color.Red, FontStyle.Regular);
        msg.Visible = false;
        autoLabel = MakeLabel("", 11, Color.DimGray, FontStyle.Regular);
        autoLabel.Visible = false;

        box = new TextBox();
        box.UseSystemPasswordChar = false; // show the password as typed
        box.Font = new Font("Segoe UI", 20);
        box.Width = 360;
        box.TextAlign = HorizontalAlignment.Center;
        box.BackColor = Color.FromArgb(30, 30, 30);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.KeyDown += BoxKeyDown;
        Controls.Add(box);

        countdownTimer = new Timer { Interval = 1000 };
        countdownTimer.Tick += delegate { remaining--; if (remaining <= 0) StartLockdown(); else UpdateCount(); };

        keepTimer = new Timer { Interval = 300 };
        keepTimer.Tick += delegate
        {
            TopMost = true; Activate();
            foreach (Process p in Process.GetProcessesByName("Taskmgr"))
                try { p.Kill(); } catch { }
            if (entryVisible) box.Focus();
        };

        autoUnlockTimer = new Timer { Interval = 1000 };
        autoUnlockTimer.Tick += delegate
        {
            autoUnlockRemaining--;
            if (autoUnlockRemaining <= 0) { allowClose = true; Close(); }
            else UpdateAutoLabel();
        };

        KeyDown += FormKeyDown;
        KeyUp += delegate (object s, KeyEventArgs e) { pressed.Remove(e.KeyCode); };
        FormClosing += delegate (object s, FormClosingEventArgs e)
        {
            if (!allowClose) e.Cancel = true; else RestorePolicies();
        };
        Resize += delegate { CenterControls(); };
        Shown += delegate
        {
            Bounds = Screen.PrimaryScreen.Bounds;
            UpdateCount();
            Activate();
            box.Focus();
            countdownTimer.Start();
        };
    }

    Label MakeLabel(string text, float size, Color color, FontStyle style)
    {
        Label l = new Label();
        l.Text = text; l.ForeColor = color; l.AutoSize = true;
        l.BackColor = Color.Transparent;
        l.Font = new Font("Segoe UI", size, style);
        Controls.Add(l);
        return l;
    }

    void CenterControls()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        if (locked)
        {
            title.Text = "This computer is locked";
            title.Font = new Font("Segoe UI", 28);
            title.Left = (w - title.Width) / 2; title.Top = (int)(h * 0.32);
            box.Left = (w - box.Width) / 2;     box.Top = (int)(h * 0.50);
            msg.Left = (w - msg.Width) / 2;     msg.Top = box.Top + 60;
            autoLabel.Left = w - autoLabel.Width - 20;
            autoLabel.Top = h - autoLabel.Height - 20;
        }
        else
        {
            title.Left = (w - title.Width) / 2; title.Top = (int)(h * 0.30);
            count.Left = (w - count.Width) / 2; count.Top = (int)(h * 0.40);
            hint.Left = (w - hint.Width) / 2;   hint.Top = (int)(h * 0.48);
            box.Left = (w - box.Width) / 2;     box.Top = (int)(h * 0.53);
            msg.Left = (w - msg.Width) / 2;     msg.Top = box.Top + 60;
        }
    }

    void UpdateCount() { count.Text = "Locking in " + remaining + "s"; CenterControls(); }

    void UpdateAutoLabel()
    {
        int m = autoUnlockRemaining / 60, s = autoUnlockRemaining % 60;
        autoLabel.Text = "Auto-unlocks in " + m + ":" + s.ToString("00");
        CenterControls();
    }

    void HideEntry()
    {
        entryVisible = false;
        box.Text = ""; box.Visible = false; msg.Visible = false;
        Cursor.Hide();
        Focus();
    }

    void StartLockdown()
    {
        locked = true;
        countdownTimer.Stop();
        hint.Visible = false; count.Visible = false; msg.Visible = false;
        LockPolicies();
        Bounds = Screen.PrimaryScreen.Bounds;
        CenterControls();
        HideEntry();
        autoLabel.Visible = true;
        UpdateAutoLabel();
        autoUnlockTimer.Start();
        keepTimer.Start();
    }

    static void LockPolicies()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SysKey))
                foreach (string v in SysVals) k.SetValue(v, 1, RegistryValueKind.DWord);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ExpKey))
                foreach (string v in ExpVals) k.SetValue(v, 1, RegistryValueKind.DWord);
        }
        catch { /* ignore - lock screen still works even if these policies can't be set */ }
    }

    static void RestorePolicies()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(SysKey, true))
                if (k != null) foreach (string v in SysVals) k.DeleteValue(v, false);
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ExpKey, true))
                if (k != null) foreach (string v in ExpVals) k.DeleteValue(v, false);
        }
        catch { }
    }

    void BoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Return) return;
        e.SuppressKeyPress = true;
        string hash;
        using (SHA256 sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(box.Text)))
                   .Replace("-", "").ToLower();
        if (hash == PasswordHash) { allowClose = true; Close(); return; }

        box.Text = ""; msg.Visible = true; CenterControls();
        if (locked)
        {
            Timer t = new Timer { Interval = 1500 };
            t.Tick += delegate { t.Stop(); t.Dispose(); HideEntry(); };
            t.Start();
        }
    }

    void FormKeyDown(object sender, KeyEventArgs e)
    {
        pressed.Add(e.KeyCode);
        if (locked && !entryVisible && pressed.Contains(Keys.V))
        {
            entryVisible = true;
            Cursor.Show();
            box.Visible = true;
            box.Focus();
            e.Handled = true;
        }
    }

    [STAThread]
    static void Main()
    {
        // Swallow any unexpected exception instead of showing a crash dialog with Quit/Continue.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate { /* ignore */ };
        AppDomain.CurrentDomain.UnhandledException += delegate { /* ignore */ };

        try
        {
            bool isNew;
            using (Mutex m = new Mutex(true, "Local\\PcLockerSingleInstance", out isNew))
            {
                if (!isNew) return;
                Application.EnableVisualStyles();
                Application.Run(new LockerForm());
            }
        }
        catch { /* ignore - never let an error surface as a crash dialog */ }
    }
}
