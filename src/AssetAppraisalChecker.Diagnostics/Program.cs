using System;

namespace AssetAppraisalChecker.Diagnostics
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return new DiagnosticRunner().Run(args, Console.Out, Console.Error);
        }
    }
}
