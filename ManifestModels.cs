using Playnite.SDK.Data;
using System.Collections.Generic;

namespace LocalGameDownloader
{
    public class ManifestRoot
    {
        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("downloads")]
        public List<ManifestDownload> Downloads { get; set; }
    }

    public class ManifestDownload
    {
        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("uploadDate")]
        public string UploadDate { get; set; }

        [SerializationPropertyName("fileSize")]
        public string FileSize { get; set; }

        [SerializationPropertyName("uris")]
        public List<string> Uris { get; set; }
    }
}
