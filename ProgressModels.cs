namespace LocalGameDownloader
{
    public class TransferProgress
    {
        public long BytesTransferred { get; }

        public long? TotalBytes { get; }

        public TransferProgress(long bytesTransferred, long? totalBytes)
        {
            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
        }
    }

    public class ExtractionProgress
    {
        public int? PercentComplete { get; }

        public ExtractionProgress(int? percentComplete)
        {
            PercentComplete = percentComplete;
        }
    }
}
