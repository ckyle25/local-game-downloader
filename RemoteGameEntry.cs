using System;
using System.Globalization;
using System.Linq;

namespace LocalGameDownloader
{
    public class RemoteGameEntry
    {
        public string CatalogName { get; set; }

        public string Name { get; set; }

        public string ArchiveFileName { get; set; }

        public string DisplayFileSize { get; set; }

        public DateTime? UploadDate { get; set; }

        public Uri DownloadUri { get; set; }

        public string UploadDateDisplay => UploadDate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;

        public string DownloadHost => DownloadUri?.Host ?? string.Empty;

        public string DisplaySummary => $"{DisplayFileSize} from {CatalogName}";

        public static RemoteGameEntry FromManifestDownload(string catalogName, ManifestDownload download)
        {
            if (!Uri.TryCreate(download.Uris.FirstOrDefault(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            DateTime parsedDate;
            return new RemoteGameEntry
            {
                CatalogName = string.IsNullOrWhiteSpace(catalogName) ? "Remote Catalog" : catalogName,
                ArchiveFileName = download.Title,
                Name = StripArchiveExtension(download.Title),
                DisplayFileSize = download.FileSize,
                UploadDate = DateTime.TryParse(download.UploadDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out parsedDate)
                    ? parsedDate
                    : (DateTime?)null,
                DownloadUri = uri
            };
        }

        private static string StripArchiveExtension(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            return title.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
                ? title.Substring(0, title.Length - 3)
                : title;
        }
    }
}
