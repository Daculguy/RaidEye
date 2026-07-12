using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace RaidEye.Agent
{
    internal sealed class NotificationDispatcher
    {
        private readonly string settingsPath;
        private readonly EventLogger logger;

        public NotificationDispatcher(string settingsPath, EventLogger logger)
        {
            this.settingsPath = settingsPath;
            this.logger = logger;
        }

        public bool HasEnabledProvider
        {
            get
            {
                AppSettings settings = SettingsStore.Load(this.settingsPath);
                return settings.NtfyEnabled || settings.WebhookEnabled;
            }
        }

        public void SendAsync(RaidEyeEvent evt)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                NotificationResult result = SendNow(evt);
                if (result.Success)
                {
                    this.logger.Write("NotificationSent", result.Message, DateTime.UtcNow, result.Detail);
                }
                else
                {
                    this.logger.Write("NotificationFailed", result.Message, DateTime.UtcNow, result.Detail);
                }
            });
        }

        public NotificationResult SendNow(RaidEyeEvent evt)
        {
            AppSettings settings = SettingsStore.Load(this.settingsPath);
            return Send(evt, settings);
        }

        private NotificationResult Send(RaidEyeEvent evt, AppSettings settings)
        {
            bool attempted = false;
            StringBuilder detail = new StringBuilder();

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            if (settings.NtfyEnabled)
            {
                attempted = true;
                NotificationResult ntfy = SendNtfy(evt, settings);
                detail.Append("ntfy: ");
                detail.Append(ntfy.Detail);
                if (ntfy.Success)
                {
                    return ntfy;
                }
            }

            if (settings.WebhookEnabled)
            {
                attempted = true;
                NotificationResult webhook = SendWebhook(evt, settings);
                if (detail.Length > 0)
                {
                    detail.Append(" | ");
                }
                detail.Append("webhook: ");
                detail.Append(webhook.Detail);
                if (webhook.Success)
                {
                    return webhook;
                }
            }

            if (!attempted)
            {
                return NotificationResult.Failed("No notification provider is enabled.", "Set ntfy_enabled=true or webhook_enabled=true in settings.ini.");
            }

            return NotificationResult.Failed("Every enabled notification provider failed.", detail.ToString());
        }

        private NotificationResult SendNtfy(RaidEyeEvent evt, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.NtfyTopic))
            {
                return NotificationResult.Failed("ntfy topic is missing.", "Set ntfy_topic in settings.ini.");
            }

            string baseUrl = string.IsNullOrWhiteSpace(settings.NtfyBaseUrl) ? "https://ntfy.sh" : settings.NtfyBaseUrl.TrimEnd('/');
            string url = baseUrl + "/" + Uri.EscapeDataString(settings.NtfyTopic.Trim());
            string body = evt.Message + Environment.NewLine + "Laptop: " + settings.EffectiveLaptopName + Environment.NewLine + "Time: " + evt.OccurredAtUtc.ToLocalTime().ToString("g");

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Timeout = settings.NotificationTimeoutSeconds * 1000;
                request.ContentType = "text/plain; charset=utf-8";
                request.Headers["Title"] = "RaidEye alert";
                request.Headers["Tags"] = "warning,computer";
                request.Headers["Priority"] = "high";
                if (!string.IsNullOrWhiteSpace(settings.NtfyBearerToken))
                {
                    request.Headers["Authorization"] = "Bearer " + settings.NtfyBearerToken.Trim();
                }

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return NotificationResult.Ok("Notification sent via ntfy.", "HTTP " + (int)response.StatusCode + " " + response.StatusDescription);
                }
            }
            catch (Exception ex)
            {
                return NotificationResult.Failed("ntfy notification failed.", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private NotificationResult SendWebhook(RaidEyeEvent evt, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.WebhookUrl))
            {
                return NotificationResult.Failed("Webhook URL is missing.", "Set webhook_url in settings.ini.");
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(settings.WebhookUrl.Trim());
                request.Method = "POST";
                request.Timeout = settings.NotificationTimeoutSeconds * 1000;
                request.ContentType = "application/json; charset=utf-8";

                if (!string.IsNullOrWhiteSpace(settings.WebhookHeaderName))
                {
                    request.Headers[settings.WebhookHeaderName.Trim()] = settings.WebhookHeaderValue ?? "";
                }

                string payload = evt.ToJson();
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return NotificationResult.Ok("Notification sent via webhook.", "HTTP " + (int)response.StatusCode + " " + response.StatusDescription);
                }
            }
            catch (Exception ex)
            {
                return NotificationResult.Failed("Webhook notification failed.", ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    internal sealed class NotificationResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; }

        public string Detail { get; private set; }

        public static NotificationResult Ok(string message, string detail)
        {
            return new NotificationResult { Success = true, Message = message, Detail = detail };
        }

        public static NotificationResult Failed(string message, string detail)
        {
            return new NotificationResult { Success = false, Message = message, Detail = detail };
        }
    }
}
