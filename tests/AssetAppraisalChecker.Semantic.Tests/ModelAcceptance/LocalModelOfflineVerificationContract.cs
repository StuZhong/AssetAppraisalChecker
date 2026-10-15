using System;
using System.IO;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelOfflineVerificationContract
    {
        internal LocalModelOfflineVerificationContract(string controlledModelRoot)
        {
            if (string.IsNullOrWhiteSpace(controlledModelRoot)
                || !Path.IsPathRooted(controlledModelRoot))
            {
                throw new ArgumentException(
                    "Controlled model root must be an absolute path.",
                    nameof(controlledModelRoot));
            }

            ControlledModelRoot = Path.GetFullPath(controlledModelRoot);
            RequireLocalAbsolutePath = true;
            AllowNetwork = false;
            AllowAutoDownload = false;
            AllowTelemetry = false;
            AllowOnlineUpdate = false;
            RequireTokenizerInitialization = true;
            RequireRepeatedInference = true;
            RequiredRepeatCount = 3;
            RequireArtifactHashBeforeAndAfter = true;
        }

        internal string ControlledModelRoot { get; }
        internal bool RequireLocalAbsolutePath { get; }
        internal bool AllowNetwork { get; }
        internal bool AllowAutoDownload { get; }
        internal bool AllowTelemetry { get; }
        internal bool AllowOnlineUpdate { get; }
        internal bool RequireTokenizerInitialization { get; }
        internal bool RequireRepeatedInference { get; }
        internal int RequiredRepeatCount { get; }
        internal bool RequireArtifactHashBeforeAndAfter { get; }

        internal void ValidateModelPath(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath) || !Path.IsPathRooted(modelPath))
            {
                throw new InvalidDataException("Model path must be absolute.");
            }

            string fullPath = Path.GetFullPath(modelPath);
            string relative = fullPath.Substring(
                Math.Min(ControlledModelRoot.Length, fullPath.Length));
            bool isRoot = string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar),
                ControlledModelRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
            bool isChild = fullPath.StartsWith(
                    ControlledModelRoot.TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                && relative.Length > 0;
            if (!isRoot && !isChild)
            {
                throw new InvalidDataException(
                    "Model path must stay inside the controlled local root.");
            }
        }
    }
}
