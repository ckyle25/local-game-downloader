using System.Globalization;
using System.IO;
using System.Linq;

namespace LocalGameDownloader
{
    public static class PathUtilities
    {
        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Game";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Game" : sanitized;
        }

        public static string FormatBytes(long bytes)
        {
            var suffixes = new[] { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            var suffixIndex = 0;
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, suffixes[suffixIndex]);
        }
    }
}
