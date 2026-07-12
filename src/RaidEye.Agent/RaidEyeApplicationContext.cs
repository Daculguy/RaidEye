using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RaidEye.Agent
{
    internal sealed class RaidEyeApplicationContext : ApplicationContext
    {
        private readonly AppSettings settings;
        private readonly EventLogger logger;
        private readonly NotificationDispatcher notifications;
        private readonly ActivityWatcher activityWatcher;
        private readonly NotifyIcon trayIcon;
        private readonly Timer pollTimer;
        private readonly ToolStripMenuItem armMenuItem;
        private readonly ToolStripMenuItem disarmMenuItem;
        private readonly ToolStripMenuItem statusMenuItem;

        private bool armed;

        public RaidEyeApplicationContext()
        {
            AppPaths.EnsureDirectory();
            SettingsStore.EnsureDefaultSettings(AppPaths.SettingsPath);

            this.settings = SettingsStore.Load(AppPaths.SettingsPath);
            this.logger = new EventLogger(AppPaths.EventsPath, this.settings.EffectiveLaptopName);
            this.notifications = new NotificationDispatcher(AppPaths.SettingsPath, this.logger);
            this.activityWatcher = new ActivityWatcher(this.settings.ArmGraceSeconds, this.settings.TriggerCooldownSeconds, this.settings.TriggerOncePerArming);
            this.activityWatcher.Triggered += OnActivityTriggered;

            this.armMenuItem = new ToolStripMenuItem("Arm RaidEye", null, OnArmClicked);
            this.disarmMenuItem = new ToolStripMenuItem("Disarm RaidEye", null, OnDisarmClicked);
            this.statusMenuItem = new ToolStripMenuItem("Status: Disarmed");
            this.statusMenuItem.Enabled = false;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(this.statusMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(this.armMenuItem);
            menu.Items.Add(this.disarmMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Test Notification", null, OnTestNotificationClicked));
            menu.Items.Add(new ToolStripMenuItem("Open Event Log", null, OnOpenEventLogClicked));
            menu.Items.Add(new ToolStripMenuItem("Open Config", null, OnOpenConfigClicked));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExitClicked));

            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "RaidEye";
            this.trayIcon.Icon = SystemIcons.Shield;
            this.trayIcon.ContextMenuStrip = menu;
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += OnArmClicked;

            this.pollTimer = new Timer();
            this.pollTimer.Interval = this.settings.PollIntervalMilliseconds;
            this.pollTimer.Tick += OnPollTimerTick;
            this.pollTimer.Start();

            this.logger.Write("AppStarted", "RaidEye tray agent started.");
            UpdateTrayState();
        }

        private void OnArmClicked(object sender, EventArgs e)
        {
            Arm();
        }

        private void OnDisarmClicked(object sender, EventArgs e)
        {
            Disarm();
        }

        private void Arm()
        {
            if (this.armed)
            {
                return;
            }

            this.armed = true;
            this.activityWatcher.Arm();
            RaidEyeEvent evt = this.logger.Write("Armed", "RaidEye armed. Watching for activity after the grace period.");
            UpdateTrayState();
            ShowBalloon("RaidEye armed", "Watching this laptop for activity.", ToolTipIcon.Info);
        }

        private void Disarm()
        {
            if (!this.armed)
            {
                return;
            }

            this.armed = false;
            this.activityWatcher.Disarm();
            this.logger.Write("Disarmed", "RaidEye disarmed.");
            UpdateTrayState();
            ShowBalloon("RaidEye disarmed", "Activity alerts are off.", ToolTipIcon.Info);
        }

        private void OnPollTimerTick(object sender, EventArgs e)
        {
            this.activityWatcher.Poll();
        }

        private void OnActivityTriggered(object sender, ActivityTriggerEventArgs e)
        {
            RaidEyeEvent evt = this.logger.Write(e.EventType, e.Message, e.OccurredAtUtc, "");
            ShowBalloon("RaidEye alert", e.Message, ToolTipIcon.Warning);
            this.notifications.SendAsync(evt);
        }

        private void OnTestNotificationClicked(object sender, EventArgs e)
        {
            RaidEyeEvent evt = this.logger.Write("TestNotification", "RaidEye test notification requested from the tray.");
            ShowBalloon("RaidEye test", "Sending a test notification if a provider is configured.", ToolTipIcon.Info);
            this.notifications.SendAsync(evt);
        }

        private void OnOpenEventLogClicked(object sender, EventArgs e)
        {
            EnsureFileExists(AppPaths.EventsPath);
            Process.Start("notepad.exe", AppPaths.EventsPath);
        }

        private void OnOpenConfigClicked(object sender, EventArgs e)
        {
            SettingsStore.EnsureDefaultSettings(AppPaths.SettingsPath);
            Process.Start("notepad.exe", AppPaths.SettingsPath);
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            this.logger.Write("AppStopped", "RaidEye tray agent stopped.");
            this.pollTimer.Stop();
            this.activityWatcher.Dispose();
            this.trayIcon.Visible = false;
            this.trayIcon.Dispose();
            this.pollTimer.Dispose();
            ExitThread();
        }

        private void UpdateTrayState()
        {
            this.armMenuItem.Enabled = !this.armed;
            this.disarmMenuItem.Enabled = this.armed;
            this.statusMenuItem.Text = this.armed ? "Status: Armed" : "Status: Disarmed";
            this.trayIcon.Icon = this.armed ? SystemIcons.Warning : SystemIcons.Shield;
            this.trayIcon.Text = this.armed ? "RaidEye - Armed" : "RaidEye - Disarmed";
        }

        private void ShowBalloon(string title, string message, ToolTipIcon icon)
        {
            try
            {
                this.trayIcon.ShowBalloonTip(4000, title, message, icon);
            }
            catch
            {
            }
        }

        private static void EnsureFileExists(string path)
        {
            AppPaths.EnsureDirectory();
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
        }
    }
}
