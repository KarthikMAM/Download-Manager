using System;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace DownloadHelper
{
    /// <summary>
    /// tracks and updates the progress of the download job
    /// </summary>
    public class DownloadTracker
    {
        //ui message formats based on DownloadState in Downloader
        private static string[] PROGRESS_MSGS = new string[]{
            "Creatring the download. . .",
            "Starting the threads. . .",
            "Download {0:f3} % complete. . .",
            "Appending files at {0:f3} MB/s. . .",
            "Aborting. . .",
            "Download Complete. . . Cleaning up",
            "Error: {0} ☹",
            "Finished. . . ✌",
            "Idle. . . Click to Resume / Start. . ."

        };

        //referance to the tracker and the downloader
        public DispatcherTimer Tracker { private set; get; }
        private Downloader downloader;

        //download progress data
        private long[] chunksSize;
        private long chunksWindowStart;

        //data of download and append jobs
        public double DwnlProgress { private set; get; }
        public long DwnlCompleted { private set; get; }
        public long DwnlSpeed { private set; get; }
        public string DwnlProgressMsg { private set; get; }

        /// <summary>
        /// initializes the download tracker object
        /// </summary>
        /// <param name="downloader"></param>
        public DownloadTracker(Downloader downloader)
        {
            //save the downloader referance
            this.downloader = downloader;

            //create a new tracker object
            Tracker = new DispatcherTimer();
            Tracker.Interval = TimeSpan.FromMilliseconds(1000);
            Tracker.Tick += Tracker_Tick;
        }

        /// <summary>
        /// state dependant tracker timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tracker_Tick(object sender, EventArgs e)
        {
            //depending on the download state update the progress message
            //collect the completion data in download and append states
            switch (downloader.State)
            {
                case Downloader.DownloadState.Download:
                    Downloading();
                    DwnlProgressMsg = string.Format(PROGRESS_MSGS[(int)downloader.State], DwnlProgress);
                    break;
                case Downloader.DownloadState.Append:
                    Appending();
                    DwnlProgressMsg = string.Format(PROGRESS_MSGS[(int)downloader.State], DwnlSpeed / (double)Downloader.MB);
                    break;
                case Downloader.DownloadState.Error:
                    DwnlProgressMsg = string.Format(PROGRESS_MSGS[(int)downloader.State], downloader.DwnlException);
                    break;
                case Downloader.DownloadState.Finish:
                    DwnlProgressMsg = PROGRESS_MSGS[(int)downloader.State];
                    DwnlSpeed = 0;
                    DwnlProgress = 100;
                    DwnlCompleted = new FileInfo(downloader.DwnlTarget).Length;
                    break;
                default:
                    DwnlProgressMsg = PROGRESS_MSGS[(int)downloader.State];
                    break;
            }
        }

        /// <summary>
        /// starts the tracker
        /// </summary>
        public void Start() { Tracker.Start(); }

        /// <summary>
        /// tracks the download progress
        /// </summary>
        private void Downloading()
        {
            //sliding windows tracker
            //monitors only necessary chunks
            if (chunksSize == null) chunksSize = new long[downloader.ChunkCount];

            DwnlSpeed = 0;
            for (long i = chunksWindowStart, j = 0; i < downloader.ChunkCount && j < downloader.ChunksActive; i++)
            {
                //new chunk size and difference found
                if (File.Exists(string.Format(downloader.ChunkTarget, i)))
                {
                    long newChunkSize = new FileInfo(string.Format(downloader.ChunkTarget, i)).Length;
                    if (newChunkSize == downloader.ChunkSize && chunksWindowStart == i) chunksWindowStart += 1;
                    DwnlSpeed += newChunkSize - chunksSize[i];
                    chunksSize[i] = newChunkSize;

                    //adjust the windows at the end
                    if (chunksSize[i] == downloader.ChunkSize) { chunksWindowStart++; }
                    else if (chunksSize[i] > 0) { j++; }
                }
            }
            DwnlCompleted += DwnlSpeed;
            DwnlProgress = (double)DwnlCompleted / downloader.DwnlSize * 100;

            //prepare for the next state
            if (downloader.State == Downloader.DownloadState.Append)
            {
                DwnlSpeed = 0;
                DwnlCompleted = 0;
                DwnlProgress = 0;
            }
        }

        /// <summary>
        /// keeps track of appending the file 
        /// </summary>
        private void Appending()
        {
            //log the progress in the old download data
            if (downloader.State == Downloader.DownloadState.Append && File.Exists(downloader.DwnlTarget))
            {
                long newFileSize = new FileInfo(downloader.DwnlTarget).Length;
                DwnlSpeed = newFileSize - DwnlCompleted;
                DwnlCompleted = newFileSize;
                DwnlProgress = (double)DwnlCompleted / downloader.DwnlSize * 100;
            }
        }
    }
}
