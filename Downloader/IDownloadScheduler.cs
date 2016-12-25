using System;

namespace Downloader
{
    public interface IDownloadScheduler
    {
        Chunks Chunks { get; }
        Exception Error { get; }

        void Abort();
        void Start();
    }
}