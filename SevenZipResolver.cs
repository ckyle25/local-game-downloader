using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalGameDownloader
{
    public class SevenZipResolver
    {
        public string Resolve(string configuredPath)
        {
            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                candidates.Add(configuredPath);
            }

            candidates.AddRange(GetPathCandidates());
            candidates.AddRange(GetDefaultInstallCandidates());
            candidates.Add(GetRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\7z.exe", string.Empty));
            candidates.Add(GetRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\7z.exe", string.Empty));
            candidates.Add(GetRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\7-Zip", "Path"));
            candidates.Add(GetRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\7-Zip", "Path"));
            candidates.Add(GetRegistryValue(RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\7-Zip", "Path"));
            candidates.Add(GetRegistryValue(RegistryHive.CurrentUser, RegistryView.Registry32, @"SOFTWARE\7-Zip", "Path"));

            return candidates
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Directory.Exists(path) ? Path.Combine(path, "7z.exe") : path)
                .FirstOrDefault(File.Exists);
        }

        private static IEnumerable<string> GetDefaultInstallCandidates()
        {
            var roots = new[]
            {
                Environment.GetEnvironmentVariable("ProgramW6432"),
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                @"C:\Program Files",
                @"C:\Program Files (x86)"
            };

            return roots
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(root => Path.Combine(root, "7-Zip", "7z.exe"));
        }

        private static IEnumerable<string> GetPathCandidates()
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                yield break;
            }

            foreach (var directory in pathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = directory.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    yield return Path.Combine(trimmed, "7z.exe");
                }
            }
        }

        private static string GetRegistryValue(RegistryHive hive, RegistryView view, string keyPath, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var key = baseKey.OpenSubKey(keyPath))
                {
                    return key?.GetValue(valueName) as string;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
