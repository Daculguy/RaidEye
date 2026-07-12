using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RaidEye.Agent
{
    internal sealed class ActivityWatcher : IDisposable
    {
        private readonly int graceSeconds;
        private readonly int cooldownSeconds;
        private readonly bool triggerOncePerArming;

        private bool armed;
        private bool disposed;
        private bool hasTriggeredThisArming;
        private DateTime armedAtUtc;
        private DateTime ignoreUntilUtc;
        private DateTime cooldownUntilUtc;
        private uint baselineInputTick;

        public event EventHandler<ActivityTriggerEventArgs> Triggered;

        public ActivityWatcher(int graceSeconds, int cooldownSeconds, bool triggerOncePerArming)
        {
            this.graceSeconds = Math.Max(0, graceSeconds);
            this.cooldownSeconds = Math.Max(1, cooldownSeconds);
            this.triggerOncePerArming = triggerOncePerArming;

            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        public void Arm()
        {
            this.armed = true;
            this.hasTriggeredThisArming = false;
            this.armedAtUtc = DateTime.UtcNow;
            this.ignoreUntilUtc = this.armedAtUtc.AddSeconds(this.graceSeconds);
            this.cooldownUntilUtc = DateTime.MinValue;
            this.baselineInputTick = WindowsActivityApi.GetLastInputTick();
        }

        public void Disarm()
        {
            this.armed = false;
            this.hasTriggeredThisArming = false;
            this.cooldownUntilUtc = DateTime.MinValue;
        }

        public void Poll()
        {
            if (!this.armed)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            uint currentInputTick = WindowsActivityApi.GetLastInputTick();

            if (nowUtc < this.ignoreUntilUtc)
            {
                this.baselineInputTick = currentInputTick;
                return;
            }

            if (currentInputTick != this.baselineInputTick)
            {
                this.baselineInputTick = currentInputTick;
                TryTrigger("ActivityDetected", "Keyboard or mouse activity detected after RaidEye was armed.");
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock ||
                e.Reason == SessionSwitchReason.SessionLogon ||
                e.Reason == SessionSwitchReason.RemoteConnect)
            {
                TryTrigger("SessionActivity", "Windows session activity detected: " + e.Reason + ".");
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                TryTrigger("PowerResume", "Laptop resumed from sleep while RaidEye was armed.");
            }
        }

        private void TryTrigger(string eventType, string message)
        {
            if (!this.armed)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc < this.ignoreUntilUtc)
            {
                return;
            }

            if (this.triggerOncePerArming && this.hasTriggeredThisArming)
            {
                return;
            }

            if (nowUtc < this.cooldownUntilUtc)
            {
                return;
            }

            this.hasTriggeredThisArming = true;
            this.cooldownUntilUtc = nowUtc.AddSeconds(this.cooldownSeconds);

            EventHandler<ActivityTriggerEventArgs> handler = this.Triggered;
            if (handler != null)
            {
                handler(this, new ActivityTriggerEventArgs(eventType, message, nowUtc));
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
    }

    internal sealed class ActivityTriggerEventArgs : EventArgs
    {
        public ActivityTriggerEventArgs(string eventType, string message, DateTime occurredAtUtc)
        {
            this.EventType = eventType;
            this.Message = message;
            this.OccurredAtUtc = occurredAtUtc;
        }

        public string EventType { get; private set; }

        public string Message { get; private set; }

        public DateTime OccurredAtUtc { get; private set; }
    }

    internal static class WindowsActivityApi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput mi;

            [FieldOffset(0)]
            public KeyboardInput ki;

            [FieldOffset(0)]
            public HardwareInput hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HardwareInput
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const ushort VirtualKeyF24 = 0x87;

        public static uint GetLastInputTick()
        {
            LastInputInfo info = new LastInputInfo();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LastInputInfo));

            if (!GetLastInputInfo(ref info))
            {
                return 0;
            }

            return info.dwTime;
        }

        public static bool SendSyntheticActivityForSelfTest()
        {
            Input[] inputs = new Input[2];
            inputs[0].type = InputKeyboard;
            inputs[0].u.ki.wVk = VirtualKeyF24;
            inputs[0].u.ki.dwFlags = 0;

            inputs[1].type = InputKeyboard;
            inputs[1].u.ki.wVk = VirtualKeyF24;
            inputs[1].u.ki.dwFlags = KeyEventKeyUp;

            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input))) == inputs.Length;
        }
    }
}
