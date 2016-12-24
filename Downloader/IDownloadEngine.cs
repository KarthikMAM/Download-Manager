using System;
using System.Threading;

namespace Downloader
{
    public interface IDownloadEngine
    {
        Exception Error { get; }
        bool IsStateCompleted { get; }
        DownloadEngine.DwnlState State { get; }

        Thread Abort();
        void Start();
    }
}