using System;
using System.IO;
using System.Security.Cryptography;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    /// <summary>Reject incomplete deployment before ORT can fall back to DLL search paths.</summary>
    public static class LocalAiDependencyCheck
    {
        public static void Verify(string applicationDirectory, string extensionsPath)
        {
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("LocalAiRequiresX64");
            // Frozen win-x64 / netstandard2.0 assets of the already approved NuGet versions.
            VerifyFile(Path.Combine(applicationDirectory, "onnxruntime.dll"), 16462648,
                "7e39e2bdbba836d98071ef28620735ba36a47c554cf794585269aecc50fab0da");
            VerifyFile(Path.Combine(applicationDirectory, "Microsoft.ML.OnnxRuntime.dll"), 234808,
                "fcfee234bc04d024945af3a46f4bdab15ef4e893559844275d5827906831c0ee");
            VerifyFile(extensionsPath, 3149880,
                "bf4eabaed00fd5465cab8ace206232d2a7fe247eb6b9408568c297fcfaaba7fc");
            foreach (string file in new[]
            {
                "AssetAppraisalChecker.Semantic.LocalRuntime.dll", "System.Memory.dll",
                "System.Buffers.dll", "System.Numerics.Vectors.dll",
                "System.Runtime.CompilerServices.Unsafe.dll", "onnxruntime_providers_shared.dll"
            })
                if (!File.Exists(Path.Combine(applicationDirectory, file)))
                    throw new FileNotFoundException("LocalAiDependencyMissing");
        }

        private static void VerifyFile(string path, long expectedSize, string expectedHash)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("LocalAiDependencyMissing");
            if (new FileInfo(path).Length != expectedSize)
                throw new InvalidDataException("LocalAiDependencyIntegrityMismatch");
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("LocalAiDependencyIntegrityMismatch");
            }
        }
    }
}
