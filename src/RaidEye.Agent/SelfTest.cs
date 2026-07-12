using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RaidEye.Agent
{
    internal static class SelfTest
    {
        public static int Run()
        {
            AppPaths.EnsureDirectory();

            string reportPath = Path.Combine(AppPaths.AppDataDirectory, "self-test.txt");
            StringBuilder report = new StringBuilder();
            report.AppendLine("RaidEye self-test");
            report.AppendLine("StartedUtc=" + DateTime.UtcNow.ToString("o"));

            try
            {
                SettingsStore.EnsureDefaultSettings(AppPaths.SettingsPath);
                AppSettings settings = SettingsStore.Load(AppPaths.SettingsPath);
                report.AppendLine("SettingsPath=" + AppPaths.SettingsPath);
                report.AppendLine("EventsPath=" + AppPaths.EventsPath);
                report.AppendLine("LaptopName=" + settings.EffectiveLaptopName);

                uint inputTick = WindowsActivityApi.GetLastInputTick();
                report.AppendLine("LastInputTick=" + inputTick);
                if (inputTick == 0)
                {
                    report.AppendLine("Result=FAIL");
                    report.AppendLine("Reason=GetLastInputInfo returned 0.");
                    File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
                    return 2;
                }

                bool watcherTriggered = false;
                using (ActivityWatcher watcher = new ActivityWatcher(0, 1, true))
                {
                    watcher.Triggered += delegate
                    {
                        watcherTriggered = true;
                    };
                    watcher.Arm();

                    bool syntheticInputSent = WindowsActivityApi.SendSyntheticActivityForSelfTest();
                    report.AppendLine("SyntheticInputSent=" + syntheticInputSent);
                    Thread.Sleep(150);
                    watcher.Poll();
                }

                report.AppendLine("WatcherTriggered=" + watcherTriggered);
                if (!watcherTriggered)
                {
                    report.AppendLine("Result=FAIL");
                    report.AppendLine("Reason=Activity watcher did not trigger after synthetic input.");
                    File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
                    return 3;
                }

                EventLogger logger = new EventLogger(AppPaths.EventsPath);
                RaidEyeEvent evt = logger.Write("SelfTest", "RaidEye self-test wrote a local event and read Windows input state.");
                report.AppendLine("EventTimestampUtc=" + evt.OccurredAtUtc.ToString("o"));

                bool webhookOk = RunWebhookSelfTest(logger, report);
                if (!webhookOk)
                {
                    report.AppendLine("Result=FAIL");
                    report.AppendLine("Reason=Webhook notification self-test failed.");
                    File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
                    return 4;
                }

                report.AppendLine("Result=PASS");
                File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
                return 0;
            }
            catch (Exception ex)
            {
                report.AppendLine("Result=FAIL");
                report.AppendLine("Exception=" + ex.GetType().Name + ": " + ex.Message);
                File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
                return 1;
            }
        }

        private static bool RunWebhookSelfTest(EventLogger logger, StringBuilder report)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            string receivedRequestLine = "";
            string receivedBody = "";
            bool received = false;

            try
            {
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                string tempSettingsPath = Path.Combine(AppPaths.AppDataDirectory, "self-test-webhook.ini");
                StringBuilder settings = new StringBuilder();
                settings.AppendLine("webhook_enabled=true");
                settings.AppendLine("webhook_url=http://127.0.0.1:" + port + "/raideye-self-test");
                settings.AppendLine("notification_timeout_seconds=5");
                File.WriteAllText(tempSettingsPath, settings.ToString(), Encoding.UTF8);

                Thread serverThread = new Thread(delegate()
                {
                    try
                    {
                        using (TcpClient client = listener.AcceptTcpClient())
                        using (NetworkStream stream = client.GetStream())
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                        {
                            receivedRequestLine = reader.ReadLine() ?? "";
                            int contentLength = 0;
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (line.Length == 0)
                                {
                                    break;
                                }

                                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                {
                                    int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
                                }
                            }

                            if (contentLength > 0)
                            {
                                char[] bodyBuffer = new char[contentLength];
                                int totalRead = 0;
                                while (totalRead < contentLength)
                                {
                                    int read = reader.Read(bodyBuffer, totalRead, contentLength - totalRead);
                                    if (read <= 0)
                                    {
                                        break;
                                    }

                                    totalRead += read;
                                }

                                receivedBody = new string(bodyBuffer, 0, totalRead);
                            }

                            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                            stream.Write(response, 0, response.Length);
                            received = true;
                        }
                    }
                    catch
                    {
                    }
                });
                serverThread.IsBackground = true;
                serverThread.Start();

                NotificationDispatcher dispatcher = new NotificationDispatcher(tempSettingsPath, logger);
                RaidEyeEvent evt = logger.Write("SelfTestWebhook", "RaidEye webhook self-test notification.");
                NotificationResult result = dispatcher.SendNow(evt);

                if (!serverThread.Join(5000))
                {
                    listener.Stop();
                    serverThread.Join(1000);
                }

                report.AppendLine("WebhookResult=" + result.Success);
                report.AppendLine("WebhookDetail=" + result.Detail);
                report.AppendLine("WebhookReceived=" + received);
                report.AppendLine("WebhookRequestLine=" + receivedRequestLine);
                report.AppendLine("WebhookBodyContainsEvent=" + receivedBody.Contains("SelfTestWebhook"));

                return result.Success && received && receivedRequestLine.StartsWith("POST /raideye-self-test", StringComparison.OrdinalIgnoreCase) && receivedBody.Contains("SelfTestWebhook");
            }
            catch (Exception ex)
            {
                report.AppendLine("WebhookException=" + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
            }
        }
    }
}
