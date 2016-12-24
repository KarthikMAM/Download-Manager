namespace Downloader
{
    public interface IDownload
    {
        double AppendProgress { get; }
        Chunks DwnlChunks { get; }
        double DwnlProgress { get; }
        long DwnlSize { get; }
        string DwnlSource { get; }
        string DwnlTarget { get; set; }
    }
}