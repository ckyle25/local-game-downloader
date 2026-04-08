using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LocalGameDownloader
{
    public class DownloadService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task DownloadFileAsync(Uri downloadUri, string destinationPath, Action<TransferProgress> progressCallback, CancellationToken cancellationToken)
        {
            using (var response = await httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        totalRead += bytesRead;
                        progressCallback?.Invoke(new TransferProgress(totalRead, totalBytes));
                    }
                }
            }
        }
    }
}
