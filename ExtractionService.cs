using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LocalGameDownloader
{
    public class ExtractionService
    {
        private static readonly Regex progressRegex = new Regex(@"(?<!\d)(\d{1,3})%", RegexOptions.Compiled);

        public async Task ExtractArchiveAsync(
            string sevenZipPath,
            string archivePath,
            string outputDirectory,
            Action<ExtractionProgress> progressCallback,
            CancellationToken cancellationToken)
        {
            var errorOutput = new StringBuilder();
            var exitSource = new TaskCompletionSource<int>();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = $"x \"{archivePath}\" -o\"{outputDirectory}\" -y -bsp1 -bso1 -bse1",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        var match = progressRegex.Match(args.Data);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
                        {
                            progressCallback?.Invoke(new ExtractionProgress(percent));
                        }
                    }
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        errorOutput.AppendLine(args.Data);
                    }
                };
                process.Exited += (_, __) => exitSource.TrySetResult(process.ExitCode);

                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start 7-Zip.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch
                    {
                    }
                }))
                {
                    var exitCode = await exitSource.Task.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (exitCode != 0)
                    {
                        var message = errorOutput.Length > 0
                            ? errorOutput.ToString().Trim()
                            : $"7-Zip failed with exit code {exitCode}.";
                        throw new InvalidOperationException(message);
                    }
                }
            }
        }
    }
}
