using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LocalGameDownloader
{
    public class ManifestService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task<IReadOnlyList<RemoteGameEntry>> LoadEntriesAsync(string manifestLocation, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(manifestLocation))
            {
                throw new InvalidOperationException("Manifest location is empty.");
            }

            string manifestContent;
            if (Uri.TryCreate(manifestLocation, UriKind.Absolute, out var manifestUri) &&
                (manifestUri.Scheme == Uri.UriSchemeHttp || manifestUri.Scheme == Uri.UriSchemeHttps))
            {
                using (var response = await httpClient.GetAsync(manifestUri, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    manifestContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            else
            {
                manifestContent = File.ReadAllText(manifestLocation);
            }

            var manifest = Serialization.FromJson<ManifestRoot>(manifestContent);
            if (manifest?.Downloads == null)
            {
                return Array.Empty<RemoteGameEntry>();
            }

            return manifest.Downloads
                .Where(download => download != null && !string.IsNullOrWhiteSpace(download.Title) && download.Uris != null && download.Uris.Any())
                .Select(download => RemoteGameEntry.FromManifestDownload(manifest.Name, download))
                .Where(download => download != null)
                .ToList();
        }
    }
}
