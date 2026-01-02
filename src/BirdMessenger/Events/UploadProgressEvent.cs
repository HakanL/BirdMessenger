namespace BirdMessenger.Delegates;

public sealed class UploadProgressEvent:UploadEvent
{
    public UploadProgressEvent(TusRequestOptionBase tusRequestOption,long? totalSize) : base(tusRequestOption)
    {
        TotalSize = totalSize;
    }
    /// <summary>
    /// indicate the size of an entire upload in bytes. May be null if the size is not known (deferred length uploads).
    /// </summary>
    public long? TotalSize { get; }

    /// <summary>
    /// indicate the size of uploaded bytes
    /// </summary>
    public long UploadedSize { get; set; }
}