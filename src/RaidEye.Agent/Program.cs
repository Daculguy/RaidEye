using System;
using System.Windows.Forms;

namespace RaidEye.Agent
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && string.Equals(args[1], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RaidEyeApplicationContext());
        }
    }
}
