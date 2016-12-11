using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Download_Manager
{
    class Downloader
    {
        //common constants
        private const int MB = 1048576;
        private const int KB = 1024;
        private static int CONN_LIMIT = 4;

        //download data
        private long dwnlSize;
        private string dwnlUrl = "https://youtube.com/";

        //chunk data
        private int chunkSize = 5 * MB;
        private int chunkCount;
        private string CHUNK_NAME = "file {0}.chunk";

        //thread data
        private int nextThread;
        private Thread[] dwnlThreads;
        private object lockObj = new object();

        //tracker data
        private long dwnlProgress;
        private long dwnlSpeed;
        private long[] chunkProgress;
        private int searchStart, searchEnd;
        private DispatcherTimer progressTracker;

        //public tracker property
        public double DwnlProgress { get { return (double)dwnlProgress / MB; } }
        public double DwnlSpeed { get { return (double)dwnlSpeed / MB; } }
        public DispatcherTimer ProgressTracker { get { return progressTracker; } }

        /// <summary>
        /// creates a task tracker and start both the tracker and the task
        /// </summary>
        public Downloader()
        {   
            //maximum allowd connection limit
            ServicePointManager.DefaultConnectionLimit = 100;

            //define and start the tracker
            progressTracker = new DispatcherTimer();
            progressTracker.Interval = TimeSpan.FromSeconds(1);
            progressTracker.Tick += ProgressTracker_Tick;
            progressTracker.Start();

            //define and start the task
            new Task(() =>
            {
                PrepareDownload();
                StartDownload();
            }).Start();
        }

        /// <summary>
        /// sets params based on the file size
        /// </summary>
        private void PrepareDownload()
        {
            //Get the file size
            HttpWebRequest sizeReq = HttpWebRequest.CreateHttp(dwnlUrl);
            sizeReq.AllowAutoRedirect = true;
            WebResponse sizeRes = sizeReq.GetResponse();
            dwnlSize = sizeRes.ContentLength;

            //Find the number of chunks to be downloaded
            if(sizeRes.Headers[HttpResponseHeader.AcceptRanges] == "bytes") { chunkCount = (int) (dwnlSize / chunkSize + (dwnlSize % chunkSize != 0 ? 1 : 0)); }
            else { chunkCount = 1; }

            //Set necessary containers
            dwnlThreads = new Thread[chunkCount];
            chunkProgress = new long[chunkCount];
        }

        /// <summary>
        /// tracks progress of download at regular intervals
        /// updates the total bytes downloaded and the avg. download speed
        /// </summary>
        /// <param name="sender">task tracker</param>
        /// <param name="e">event args sent by the task tracker</param>
        private void ProgressTracker_Tick(object sender, EventArgs e)
        {
            //this uses a modified sliding window
            //if at the end a next file exists expand the window
            //if the start thread finishes reduce the windows
                        
            dwnlSpeed = 0;
            for (int i = searchStart; i <= searchEnd; i++)
            {
                if (i == searchEnd)
                {
                    //if a new thread started its work expand the window and resample this thread
                    if (File.Exists(String.Format(CHUNK_NAME, i))) { searchEnd += 1; i -= 1; }
                }
                else
                {
                    //get the progress of the thread and add it to the overall progress
                    long newProgress = new FileInfo(String.Format(CHUNK_NAME, i)).Length;
                    if (newProgress == chunkSize && searchStart == i) searchStart += 1; // changed from se - i = 1
                    dwnlSpeed += newProgress - chunkProgress[i];
                    chunkProgress[i] = newProgress;
                }
            }
            dwnlProgress += dwnlSpeed;

            //stop tracker when job is complete
            if (dwnlProgress == dwnlSize) progressTracker.Stop();
        }

        /// <summary>
        /// starts the seed threads which upon 
        /// completion will start next thread in the queue
        /// and waits till all the threads have finished work
        /// this blocks till completion
        /// </summary>
        private void StartDownload()
        {
            nextThread = Math.Min(CONN_LIMIT, chunkCount);
            for (int i = 0; i < chunkCount; i++) dwnlThreads[i] = new Thread(new ParameterizedThreadStart(DownloadChunk));
            lock (lockObj)
            {
                for (int i = 0; i < nextThread; i++)
                    dwnlThreads[i].Start(i);
            }
            for (int i = 0; i < chunkCount; i++) dwnlThreads[i].Join();
        }

        /// <summary>
        /// downloads the 0-indexed chunks
        /// adjusts chunk start and end position to allow resumin
        /// performs a blocking download of the chunk
        /// </summary>
        /// <param name="chunkId">0-indexed chunk id</param>
        private void DownloadChunk(object chunkId)
        {
            int chunkNo = (Int32)chunkId;
            string chunkName = string.Format(CHUNK_NAME, chunkNo);
            
            //adjust range to facilitate resuming
            long chunkStart = chunkNo * chunkSize;
            long chunkEnd = (chunkNo == chunkCount - 1 ? dwnlSize : chunkStart + chunkSize) - 1;
            if (File.Exists(chunkName)) { chunkStart += new FileInfo(chunkName).Length; }

            
            if ( chunkStart < chunkEnd )
            {
                //prepare download request
                HttpWebRequest dwnlReq = WebRequest.CreateHttp(dwnlUrl);
                dwnlReq.AddRange(chunkStart, chunkEnd);
                dwnlReq.AllowAutoRedirect = true;
                
                //download the chunk
                BufferedStream dwnlSource = new BufferedStream(dwnlReq.GetResponse().GetResponseStream());
                BufferedStream dwnlTarget = new BufferedStream(new FileStream(chunkName, FileMode.Append, FileAccess.Write));
                dwnlSource.CopyTo(dwnlTarget);
                dwnlSource.Close();
                dwnlTarget.Close();
            }

            //start the next thread without overlapping updates
            lock (lockObj)
            {
                if (nextThread < chunkCount) dwnlThreads[nextThread].Start(nextThread++);
            }
        }
    }
}
