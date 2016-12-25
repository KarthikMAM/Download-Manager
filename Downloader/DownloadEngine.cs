using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Downloader
{
    /// <summary>
    /// download engine executes a download job
    /// </summary>
    public class DownloadEngine : IDownloadEngine
    {
        //the download the engine operates on
        public Download Download;

        //engine state properties
        public enum DwnlState { Create, Idle, Start, Download, Append, Complete, Error, Abort }
        public DwnlState State { private set; get; }
        public bool IsStateCompleted { private set; get; }
        public Exception Error { private set; get; }

        //engine workers
        private ChunkDownloader[] chunkDownloaders;
        private Thread workerThread;
        private long threadLimit = 10;

        //engine trackers
        private DispatcherTimer downloadTracker;
        private long trackerWindowStart, trackerWindowEnd;

        /// <summary>
        /// create a new engine for the download
        /// </summary>
        /// <param name="download">download to operate on</param>
        /// <param name="downloadTracker">tracker to monitor the download optional</param>
        public DownloadEngine(Download download, DispatcherTimer downloadTracker = null)
        {
            try
            {
                IsStateCompleted = false; State = DwnlState.Create;

                //save the download job
                Download = download;

                //create a downloader for each chunk
                chunkDownloaders = new ChunkDownloader[Download.DwnlChunks.ChunkCount];
                for (long i = 0; i < Download.DwnlChunks.ChunkCount; i++)
                {
                    chunkDownloaders[i] = new ChunkDownloader(Download.DwnlChunks, i);
                }

                //if there is a tracker then add a tick to it
                if (downloadTracker != null)
                {
                    this.downloadTracker = downloadTracker;
                    this.downloadTracker.Tick += DownloadTracker_Tick;
                }

                IsStateCompleted = false; State = DwnlState.Idle;
            }
            catch (Exception e)
            {
                IsStateCompleted = false; State = DwnlState.Error;

                Error = e;

                IsStateCompleted = true;
            }
        }

        /// <summary>
        /// tracks the progress of the download
        /// </summary>
        /// <param name="sender">which requests tracking on the download</param>
        /// <param name="e"></param>
        private void DownloadTracker_Tick(object sender, EventArgs e)
        {
            switch (State)
            {
                case DwnlState.Download:
                case DwnlState.Append:
                case DwnlState.Idle:
                case DwnlState.Complete:

                    //find the size of completed chunks
                    long windowStart = Interlocked.Read(ref trackerWindowStart);
                    long windowEnd = Interlocked.Read(ref trackerWindowEnd);
                    double newDwnlSizeCompleted = windowStart * Download.DwnlChunks.ChunkSize;

                    //update the size of the active chunks
                    for (long i = windowStart; i < windowEnd; i++)
                    {
                        double newChunkSizeCompleted = Interlocked.Read(ref Download.DwnlChunks.ChunkProgress[i]);
                        newDwnlSizeCompleted += newChunkSizeCompleted;
                    }

                    //compute the speed and progress
                    Download.DwnlSpeed = Math.Max(0, (newDwnlSizeCompleted - Download.DwnlSizeCompleted) / ((DispatcherTimer)sender).Interval.Seconds);
                    Download.DwnlSizeCompleted = newDwnlSizeCompleted;

                    break;
            }
        }

        /// <summary>
        /// starts the worker thread for the download job
        /// </summary>
        public void Start()
        {
            if (State == DwnlState.Idle)
            {
                IsStateCompleted = false; State = DwnlState.Start;

                //things to reset before starting
                trackerWindowStart = trackerWindowEnd = 0;
                Error = null;

                //the flow of various tasks within the worker thread
                workerThread = new Thread(() =>
                {
                    try
                    {
                        IsStateCompleted = false; State = DwnlState.Download;

                        Downloading();

                        IsStateCompleted = false; State = DwnlState.Append;

                        Appending();

                        IsStateCompleted = false; State = DwnlState.Complete;

                        Completing();

                        IsStateCompleted = true;
                    }
                    catch (ThreadAbortException) { /* ignore this exception */ }
                    catch (Exception e)
                    {
                        IsStateCompleted = false; State = DwnlState.Abort;

                        Aborting();

                        IsStateCompleted = false; State = DwnlState.Error;

                        Error = e;

                        IsStateCompleted = true;
                    }
                });

                //start the worker thread
                workerThread.Start();
            }
        }

        /// <summary>
        /// aborts the download engine async
        /// </summary>
        public Thread Abort()
        {
            IsStateCompleted = false; State = DwnlState.Abort;

            //create a new thread to abort the engine
            Thread abortThread = new Thread(() =>
            {
                Aborting();

                IsStateCompleted = true; State = DwnlState.Idle;
            });

            abortThread.Start();
            return abortThread;

        }

        /// <summary>
        /// starts individual downloaders and waits for them
        /// </summary>
        private void Downloading()
        {
            //starts n threads allowed by the limit
            //updates the tracker window
            long nextChunk;
            for (nextChunk = 0; nextChunk < Math.Min(threadLimit, Download.DwnlChunks.ChunkCount); nextChunk++)
            {
                chunkDownloaders[nextChunk].Start();

                Interlocked.Increment(ref trackerWindowEnd);
            }

            //waits for the download threads to finish
            //updates the tracker window
            //starts next thread if it is okay
            for (int i = 0; i < Download.DwnlChunks.ChunkCount; i++, nextChunk++)
            {
                chunkDownloaders[i].Join();
                Interlocked.Increment(ref trackerWindowStart);

                if (State == DwnlState.Abort) Thread.CurrentThread.Abort();
                else if (nextChunk < Download.DwnlChunks.ChunkCount)
                {
                    chunkDownloaders[nextChunk].Start();
                    Interlocked.Increment(ref trackerWindowEnd);
                }
            }

            //Thread[] schedulerThreads = new Thread[Math.Min(threadLimit, Download.DwnlChunks.ChunkCount)];
            //for (long i = 0, nextChunk = 0; i < schedulerThreads.LongLength; i++)
            //{
            //    schedulerThreads[i] = new Thread(() => {
            //        try
            //        {
            //            while (true)
            //            {
            //                long currentChunk = 0;
            //                lock (Download)
            //                {
            //                    currentChunk = nextChunk++;
            //                    chunkDownloaders[currentChunk].Start();
            //                }

            //                chunkDownloaders[currentChunk].Join();

            //                lock (Download)
            //                {
            //                    if (State == DwnlState.Abort || nextChunk >= Download.DwnlChunks.ChunkCount)
            //                    {
            //                        break;
            //                    }
            //                }
            //            }
            //        }
            //        catch (Exception e)
            //        {
            //            Error = e;
            //        }
            //    });
            //}
        }

        /// <summary>
        /// stitches the chunks together
        /// </summary>
        private void Appending()
        {
            //synchronus copy of chunks to target file
            using (BufferedStream TargetFile = new BufferedStream(new FileStream(Download.DwnlTarget, FileMode.Create, FileAccess.Write)))
                for (int i = 0; i < Download.DwnlChunks.ChunkCount; i++)
                    using (BufferedStream SourceChunks = new BufferedStream(File.OpenRead(Download.DwnlChunks.ChunkTarget(i))))
                        SourceChunks.CopyTo(TargetFile);
        }

        /// <summary>
        /// performs cleanup jobs
        /// </summary>
        private void Completing()
        {
            //cleanup job 1: delete the chunks
            for (int i = 0; i < Download.DwnlChunks.ChunkCount; i++)
                File.Delete(Download.DwnlChunks.ChunkTarget(i));

            //cleanup job 2: delete the chunk directory
            Directory.Delete(Path.GetDirectoryName(Download.DwnlChunks.ChunkTarget(0)));
        }

        /// <summary>
        /// aborts the worker and the download threads
        /// </summary>
        private void Aborting()
        {
            //stop all the thread in sync mode
            if (workerThread.IsAlive)
            {
                for (long i = 0; i < Download.DwnlChunks.ChunkCount; i++)
                {
                    chunkDownloaders[i].Abort();
                }
            }
        }
    }
}
