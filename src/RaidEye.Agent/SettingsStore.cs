using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RaidEye.Agent
{
    internal static class SettingsStore
    {
        public static void EnsureDefaultSettings(string path)
        {
            AppPaths.EnsureDirectory();
            if (File.Exists(path))
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# RaidEye local settings");
            builder.AppendLine("laptop_name=");
            builder.AppendLine("arm_grace_seconds=5");
            builder.AppendLine("poll_interval_ms=1000");
            builder.AppendLine("trigger_once_per_arming=true");
            builder.AppendLine("trigger_cooldown_seconds=300");
            builder.AppendLine("");
            builder.AppendLine("ntfy_enabled=false");
            builder.AppendLine("ntfy_base_url=https://ntfy.sh");
            builder.AppendLine("ntfy_topic=");
            builder.AppendLine("ntfy_bearer_token=");
            builder.AppendLine("");
            builder.AppendLine("webhook_enabled=false");
            builder.AppendLine("webhook_url=");
            builder.AppendLine("webhook_header_name=");
            builder.AppendLine("webhook_header_value=");
            builder.AppendLine("");
            builder.AppendLine("notification_timeout_seconds=10");

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        public static AppSettings Load(string path)
        {
            EnsureDefaultSettings(path);

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }

                int equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, equalsIndex).Trim();
                string value = line.Substring(equalsIndex + 1).Trim();
                values[key] = value;
            }

            AppSettings settings = new AppSettings();
            settings.LaptopName = GetString(values, "laptop_name", "");
            settings.ArmGraceSeconds = GetInt(values, "arm_grace_seconds", 5, 0, 300);
            settings.PollIntervalMilliseconds = GetInt(values, "poll_interval_ms", 1000, 250, 60000);
            settings.TriggerOncePerArming = GetBool(values, "trigger_once_per_arming", true);
            settings.TriggerCooldownSeconds = GetInt(values, "trigger_cooldown_seconds", 300, 1, 86400);

            settings.NtfyEnabled = GetBool(values, "ntfy_enabled", false);
            settings.NtfyBaseUrl = GetString(values, "ntfy_base_url", "https://ntfy.sh");
            settings.NtfyTopic = GetString(values, "ntfy_topic", "");
            settings.NtfyBearerToken = GetString(values, "ntfy_bearer_token", "");

            settings.WebhookEnabled = GetBool(values, "webhook_enabled", false);
            settings.WebhookUrl = GetString(values, "webhook_url", "");
            settings.WebhookHeaderName = GetString(values, "webhook_header_name", "");
            settings.WebhookHeaderValue = GetString(values, "webhook_header_value", "");

            settings.NotificationTimeoutSeconds = GetInt(values, "notification_timeout_seconds", 10, 1, 120);
            return settings;
        }

        private static string GetString(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            if (values.TryGetValue(key, out value))
            {
                return value;
            }

            return fallback;
        }

        private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            if (!values.TryGetValue(key, out value))
            {
                return fallback;
            }

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fallback;
        }

        private static int GetInt(Dictionary<string, string> values, string key, int fallback, int min, int max)
        {
            string value;
            int parsed;
            if (!values.TryGetValue(key, out value) || !int.TryParse(value, out parsed))
            {
                return fallback;
            }

            if (parsed < min)
            {
                return min;
            }

            if (parsed > max)
            {
                return max;
            }

            return parsed;
        }
    }

    internal sealed class AppSettings
    {
        public string LaptopName { get; set; }

        public int ArmGraceSeconds { get; set; }

        public int PollIntervalMilliseconds { get; set; }

        public bool TriggerOncePerArming { get; set; }

        public int TriggerCooldownSeconds { get; set; }

        public bool NtfyEnabled { get; set; }

        public string NtfyBaseUrl { get; set; }

        public string NtfyTopic { get; set; }

        public string NtfyBearerToken { get; set; }

        public bool WebhookEnabled { get; set; }

        public string WebhookUrl { get; set; }

        public string WebhookHeaderName { get; set; }

        public string WebhookHeaderValue { get; set; }

        public int NotificationTimeoutSeconds { get; set; }

        public string EffectiveLaptopName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.LaptopName))
                {
                    return this.LaptopName.Trim();
                }

                return Environment.MachineName;
            }
        }
    }
}
