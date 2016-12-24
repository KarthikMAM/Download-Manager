namespace Downloader
{
    public interface IChunkDownloader
    {
        void Abort();
        void Download();
        void Join();
        void Start();
    }
}