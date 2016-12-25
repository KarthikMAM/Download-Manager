using System;
using System.IO;
using System.Threading;
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

        //engine worker
        private Thread workerThread;

        //engine trackers
        private DispatcherTimer downloadTracker;

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
                case DwnlState.Complete:
                case DwnlState.Download:
                    if (Download.DwnlProgress < 100)
                    {
                        TimeSpan timeSpan = ((DispatcherTimer)sender).Interval;
                        Download.UpdateDownloadProgress(timeSpan.Seconds + (double)timeSpan.Milliseconds / 1000);
                    }
                    break;
                case DwnlState.Append:

                    Download.UpdateAppendProgress();

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
                Error = null;

                //the flow of various tasks within the worker thread
                workerThread = new Thread(() =>
                {
                    try
                    {
                        IsStateCompleted = false; State = DwnlState.Download;

                        Download.DwnlScheduler.Start();

                        IsStateCompleted = false; State = DwnlState.Append;

                        Append();

                        IsStateCompleted = false; State = DwnlState.Complete;

                        Complete();

                        IsStateCompleted = true;
                    }
                    catch (ThreadAbortException) { /* ignore this exception */ }
                    catch (Exception e)
                    {
                        IsStateCompleted = false; State = DwnlState.Abort;

                        Abort().Join();

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
                if (workerThread.IsAlive)
                {
                    Download.DwnlScheduler.Abort();
                }

                IsStateCompleted = true; State = DwnlState.Idle;
            });

            abortThread.Start();
            return abortThread;

        }

        /// <summary>
        /// stitches the chunks together
        /// </summary>
        private void Append()
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
        private void Complete()
        {
            //cleanup job 1: delete the chunks
            for (int i = 0; i < Download.DwnlChunks.ChunkCount; i++)
                File.Delete(Download.DwnlChunks.ChunkTarget(i));

            //cleanup job 2: delete the chunk directory
            Directory.Delete(Path.GetDirectoryName(Download.DwnlChunks.ChunkTarget(0)));
        }
    }
}
