using System;
using System.IO;
using System.Text;

namespace RaidEye.Agent
{
    internal sealed class EventLogger
    {
        private readonly object gate = new object();
        private readonly string path;
        private readonly string laptopName;

        public EventLogger(string path)
            : this(path, Environment.MachineName)
        {
        }

        public EventLogger(string path, string laptopName)
        {
            this.path = path;
            this.laptopName = string.IsNullOrWhiteSpace(laptopName) ? Environment.MachineName : laptopName.Trim();
            AppPaths.EnsureDirectory();
        }

        public string Path
        {
            get { return this.path; }
        }

        public RaidEyeEvent Write(string eventType, string message)
        {
            return Write(eventType, message, DateTime.UtcNow, null);
        }

        public RaidEyeEvent Write(string eventType, string message, DateTime occurredAtUtc, string detail)
        {
            RaidEyeEvent evt = new RaidEyeEvent();
            evt.EventType = eventType;
            evt.Message = message;
            evt.Detail = detail ?? "";
            evt.OccurredAtUtc = occurredAtUtc;
            evt.LaptopName = this.laptopName;

            string line = evt.ToJson();
            lock (this.gate)
            {
                File.AppendAllText(this.path, line + Environment.NewLine, Encoding.UTF8);
            }

            return evt;
        }
    }

    internal sealed class RaidEyeEvent
    {
        public string EventType { get; set; }

        public string Message { get; set; }

        public string Detail { get; set; }

        public string LaptopName { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public string ToJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            AppendJsonProperty(builder, "timestampUtc", this.OccurredAtUtc.ToString("o"), true);
            AppendJsonProperty(builder, "eventType", this.EventType, false);
            AppendJsonProperty(builder, "laptopName", this.LaptopName, false);
            AppendJsonProperty(builder, "message", this.Message, false);
            AppendJsonProperty(builder, "detail", this.Detail, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool first)
        {
            if (!first)
            {
                builder.Append(",");
            }

            builder.Append("\"");
            builder.Append(EscapeJson(name));
            builder.Append("\":\"");
            builder.Append(EscapeJson(value ?? ""));
            builder.Append("\"");
        }

        private static string EscapeJson(string value)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
