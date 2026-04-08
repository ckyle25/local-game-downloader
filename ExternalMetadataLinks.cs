using System;

namespace LocalGameDownloader
{
    public static class ExternalMetadataLinks
    {
        public static Uri BuildIgdbSearchUri(string gameName)
        {
            return BuildSearchUri("https://www.igdb.com/search?utf8=%E2%9C%93&type=1&q=", gameName);
        }

        public static Uri BuildPcGamingWikiSearchUri(string gameName)
        {
            return BuildSearchUri("https://www.pcgamingwiki.com/w/index.php?search=", gameName);
        }

        private static Uri BuildSearchUri(string baseUri, string gameName)
        {
            var escapedName = Uri.EscapeDataString(gameName ?? string.Empty);
            return new Uri($"{baseUri}{escapedName}", UriKind.Absolute);
        }
    }
}
