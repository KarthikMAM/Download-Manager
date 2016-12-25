namespace Downloader
{
    public interface IDownload
    {
        double AppendProgress { get; }
        Chunks DwnlChunks { get; }
        double DwnlProgress { get; }
        DownloadScheduler DwnlScheduler { get; }
        long DwnlSize { get; }
        double DwnlSizeCompleted { get; }
        string DwnlSource { get; }
        double DwnlSpeed { get; }
        string DwnlTarget { get; set; }

        void UpdateAppendProgress();
        void UpdateDownloadProgress(double timeSpan);
    }
}