using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace LocalGameDownloader
{
    public class GameImportService
    {
        private const string DefaultPlatformName = "PC (Windows)";

        public Game ImportInstalledGame(
            IPlayniteAPI api,
            RemoteGameEntry entry,
            string installDirectory,
            MetadataEnrichmentService metadataEnrichmentService = null)
        {
            var selectedExecutable = ResolveExecutable(api, entry, installDirectory);
            if (string.IsNullOrWhiteSpace(selectedExecutable))
            {
                throw new InvalidOperationException("No playable executable was selected.");
            }

            var metadata = new GameMetadata
            {
                Name = entry.Name,
                SortingName = entry.Name,
                InstallDirectory = installDirectory,
                IsInstalled = true,
                Source = new MetadataNameProperty(entry.CatalogName),
                GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        Name = "Play",
                        Type = GameActionType.File,
                        Path = selectedExecutable,
                        WorkingDir = Path.GetDirectoryName(selectedExecutable),
                        IsPlayAction = true
                    }
                },
                Platforms = new HashSet<MetadataProperty>
                {
                    new MetadataNameProperty(DefaultPlatformName)
                }
            };

            metadataEnrichmentService?.TryApplyMetadata(api, metadata);

            var importedGame = api.Database.ImportGame(metadata);
            TrySetIconFromExecutable(api, importedGame, selectedExecutable);
            return importedGame;
        }

        private string ResolveExecutable(IPlayniteAPI api, RemoteGameEntry entry, string installDirectory)
        {
            var candidates = Directory.EnumerateFiles(installDirectory, "*.exe", SearchOption.AllDirectories)
                .Select(path => new ExecutableCandidate(path, ScoreExecutable(path, entry.Name, installDirectory)))
                .Where(candidate => candidate.Score > -100)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Path.Length)
                .ToList();

            if (!candidates.Any())
            {
                throw new InvalidOperationException("No executable files were found after extraction.");
            }

            if (candidates.Count == 1 || candidates[0].Score >= candidates[1].Score + 25)
            {
                return candidates[0].Path;
            }

            var options = candidates
                .Select(candidate => new GenericItemOption(
                    Path.GetFileName(candidate.Path),
                    candidate.Path.Substring(installDirectory.Length).TrimStart(Path.DirectorySeparatorChar)))
                .ToList();

            var selected = api.Dialogs.ChooseItemWithSearch(
                options,
                searchTerm => options
                    .Where(option =>
                        option.Name.IndexOf(searchTerm ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        option.Description.IndexOf(searchTerm ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList(),
                "Select the executable Playnite should launch.",
                "Choose Game Executable");

            if (selected == null)
            {
                return null;
            }

            return candidates[options.IndexOf(selected)].Path;
        }

        private static int ScoreExecutable(string executablePath, string gameName, string installDirectory)
        {
            var fileName = Path.GetFileNameWithoutExtension(executablePath) ?? string.Empty;
            var relativePath = executablePath.Substring(installDirectory.Length).ToLowerInvariant();
            var normalizedGameName = Normalize(gameName);
            var normalizedFileName = Normalize(fileName);

            var score = 0;
            if (normalizedFileName == normalizedGameName)
            {
                score += 100;
            }

            if (!string.IsNullOrWhiteSpace(normalizedGameName) && normalizedFileName.Contains(normalizedGameName))
            {
                score += 60;
            }

            if (relativePath.Contains("bin") || relativePath.Contains("win64") || relativePath.Contains("binaries"))
            {
                score += 15;
            }

            if (relativePath.Contains("commonredist") || relativePath.Contains("redist") || relativePath.Contains("support"))
            {
                score -= 150;
            }

            if (normalizedFileName.StartsWith("unins") ||
                normalizedFileName.StartsWith("setup") ||
                normalizedFileName.Contains("crashreport"))
            {
                score -= 100;
            }

            return score;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static void TrySetIconFromExecutable(IPlayniteAPI api, Game game, string executablePath)
        {
            if (game == null || string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(game.Icon))
            {
                return;
            }

            try
            {
                using (var icon = Icon.ExtractAssociatedIcon(executablePath))
                {
                    if (icon == null)
                    {
                        return;
                    }

                    var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ico");
                    try
                    {
                        using (var stream = File.OpenWrite(tempPath))
                        {
                            icon.Save(stream);
                        }

                        var iconId = api.Database.AddFile(tempPath, game.Id);
                        if (!string.IsNullOrWhiteSpace(iconId))
                        {
                            game.Icon = iconId;
                            api.Database.Games.Update(game);
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private class ExecutableCandidate
        {
            public string Path { get; }

            public int Score { get; }

            public ExecutableCandidate(string path, int score)
            {
                Path = path;
                Score = score;
            }
        }
    }
}
