using System;
using System.IO;

namespace AssetAppraisalChecker.Tests.Desktop
{
    // Explicit interactive entry, never run by the ordinary unit-test suite.
    // Compile against Tests.dll into the ignored test bin directory.
    public static class WpsNavigationSmokeLauncher
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string output = args.Length == 1 ? args[0] : Path.Combine(Path.GetTempPath(),
                "AssetAppraisalChecker-WpsNavigation-" + Guid.NewGuid().ToString("N"));
            Console.WriteLine("Synthetic evidence directory: " + output);
            try { Console.WriteLine(WpsNavigationSmoke.Run(output)); return 0; }
            catch (Exception e)
            {
                Console.Error.WriteLine("Smoke failed: " + e.GetType().Name + " / " + e.Message);
                return 1;
            }
        }
    }
}
