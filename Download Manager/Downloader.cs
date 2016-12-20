using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Download_Manager
{
    class Downloader
    {
        //common constants
        private const int MB = 1048576;
        private const int KB = 1024;

        //download data
        private long dwnlSize;
        private string dwnlPath;
        private string targetPath;

        //chunk data
        private int chunkSize;
        private int chunkCount;
        private string chunkPath = "file {0}.chunk";

        //thread data
        private int nextThread;
        private Thread[] dwnlThreads;
        private object lockObj = new object();
        private bool abortFlag = false;
        private int activeThreads = 4;

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
        public double DwnlSize { get { return (double)dwnlSize / MB; } }


        /// <summary>
        /// creates a task tracker and sets download data
        /// </summary>
        /// <param name="dwnlPath">url of the download</param>
        /// <param name="targetPath">location to save the download</param>
        /// <param name="chunkSize">maximum chunk size limit</param>
        /// <param name="activeThreads">maximum number of active threads</param>
        public Downloader(string dwnlPath, string targetPath, int chunkSize, int activeThreads)
        {
            //define the required parameters
            ServicePointManager.DefaultConnectionLimit = 30;
            this.dwnlPath = dwnlPath;
            this.targetPath = targetPath;
            this.chunkSize = chunkSize * MB;
            this.activeThreads = activeThreads;
            
            //define the tracker
            progressTracker = new DispatcherTimer();
            progressTracker.Interval = TimeSpan.FromSeconds(1);
            progressTracker.Tick += ProgressTracker_Tick;
        }

        /// <summary>
        /// starts the task tracker
        /// creates a download task and starts it
        /// </summary>
        public void Start()
        {
            //start the tracker
            progressTracker.Start();

            //define and start the task
            //handle exceptions and finally stop
            new Task(() =>
            {
                try
                {
                    PrepareTask();
                    StartTask();
                    MessageBox.Show("Download saved as " + targetPath, "Download Complete!");
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error: " + e.Message, "Download Failed", MessageBoxButton.OK);
                }
                finally
                {
                    Stop();
                }
            }).Start();
        }

        /// <summary>
        /// set the abort flag to true to stop downloads
        /// </summary>
        public void Stop() { abortFlag = true; }

        /// <summary>
        /// delete the downloaded chunks
        /// and the associated directories
        /// </summary>
        public void Delete()
        {
            if (File.Exists(String.Format(chunkPath, 0)))
                Directory.Delete(Path.GetDirectoryName(chunkPath), true);
        }

        /// <summary>
        /// append the chunks together
        /// </summary>
        private void AppendChunks()
        {
            using (BufferedStream targetFile = new BufferedStream(new FileStream(targetPath, FileMode.OpenOrCreate, FileAccess.Write)))
            {
                for (int i = 0; i < chunkCount; i++)
                {
                    //if chunk is completely downloaded save it
                    //else raise exception
                    if (new FileInfo(String.Format(chunkPath, i)).Length == chunkSize || i == chunkCount - 1)
                    {
                        BufferedStream sourceFile = new BufferedStream(new FileStream(String.Format(chunkPath, i), FileMode.Open, FileAccess.Read));
                        sourceFile.CopyTo(targetFile);
                        sourceFile.Close();
                        targetFile.Flush();
                    }
                    else
                    {
                        throw new Exception("Corrupted chunks detected");
                    }
                }
            }
        }

        /// <summary>
        /// sets params based on the file size
        /// </summary>
        private void PrepareTask()
        {
            //get the file size
            HttpWebRequest sizeReq = HttpWebRequest.CreateHttp(dwnlPath);
            sizeReq.AllowAutoRedirect = true;
            WebResponse sizeRes = sizeReq.GetResponse();
            dwnlSize = sizeRes.ContentLength;

            //find the number of chunks to be downloaded
            if (sizeRes.Headers[HttpResponseHeader.AcceptRanges] == "bytes")
            {
                chunkCount = (int)(dwnlSize / chunkSize + (dwnlSize % chunkSize != 0 ? 1 : 0));
            }
            else
            {
                chunkCount = 1;
            }

            //set necessary containers
            dwnlThreads = new Thread[chunkCount];
            chunkProgress = new long[chunkCount];

            //create the directory environment
            chunkPath = Path.Combine((uint)targetPath.GetHashCode() + "", chunkPath);
            if (File.Exists(String.Format(chunkPath, 0)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(chunkPath) + "");
            }
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
                    if (File.Exists(String.Format(chunkPath, i))) { searchEnd += 1; i -= 1; }
                }
                else
                {
                    //get the progress of the thread and add it to the overall progress
                    long newProgress = new FileInfo(String.Format(chunkPath, i)).Length;
                    if (newProgress == chunkSize && searchStart == i) searchStart += 1; // changed from se - i = 1
                    dwnlSpeed += newProgress - chunkProgress[i];
                    chunkProgress[i] = newProgress;
                }
            }
            dwnlProgress += dwnlSpeed;

            //stop tracker when job is complete
            if (abortFlag == true) StopTask();
        }

        /// <summary>
        /// starts the seed threads which upon 
        /// completion will start next thread in the queue
        /// and blocks till all the threads have finished work
        /// appends the downloaded chunk
        /// </summary>
        private void StartTask()
        {
            //start threads and wait till job completion
            nextThread = Math.Min(activeThreads, chunkCount);
            for (int i = 0; i < chunkCount; i++)
                dwnlThreads[i] = new Thread(new ParameterizedThreadStart(DownloadChunk));

            //acquire lock to streamline the start of the thread
            lock (lockObj)
            {
                for (int i = 0; i < nextThread; i++)
                    dwnlThreads[i].Start(i);
            }

            //wait for all the started threads to complete
            for (int i = 0; i < chunkCount; i++)
                if (dwnlThreads[i].ThreadState != ThreadState.Unstarted)
                    dwnlThreads[i].Join();

            //if not aborted append the chunks
            //otherwise raise an error
            if (abortFlag != true)
                AppendChunks();
            else
                throw new Exception("Download aborted by the user");
        }

        /// <summary>
        /// stops the download task and its tracker
        /// delete the cache when download is complete
        /// </summary>
        private void StopTask()
        {
            if (progressTracker.IsEnabled == true)
            {
                //stop the tracker
                progressTracker.Stop();

                //stop the running threads which are in the window
                if (dwnlThreads != null)
                {
                    for (int i = searchStart, aborts = 0; i < chunkCount && aborts < activeThreads; i++)
                    {
                        if (dwnlThreads[i].ThreadState == ThreadState.Running)
                        {
                            dwnlThreads[i].Abort();
                            aborts++;
                        }
                    }
                }

                //if the download is complete delete the chunks
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length == dwnlSize) Delete();
            }
        }

        /// <summary>
        /// downloads the 0-indexed chunks
        /// adjusts chunk start and end position to allow resumin
        /// performs a blocking download of the chunk
        /// </summary>
        /// <param name="chunkId">0-indexed chunk id</param>
        private void DownloadChunk(object chunkId)
        {
            //chunk properties
            int chunkNo = (Int32)chunkId;
            string chunkFile = string.Format(chunkPath, chunkNo);

            //adjust range to facilitate resuming
            long chunkStart = chunkNo * chunkSize;
            long chunkEnd = (chunkNo == chunkCount - 1 ? dwnlSize : chunkStart + chunkSize) - 1;
            if (File.Exists(chunkFile)) { chunkStart += new FileInfo(chunkFile).Length; }


            if (chunkStart < chunkEnd)
            {
                //prepare download request
                HttpWebRequest dwnlReq = WebRequest.CreateHttp(dwnlPath);
                dwnlReq.AddRange(chunkStart, chunkEnd);
                dwnlReq.AllowAutoRedirect = true;

                //download the chunk
                BufferedStream dwnlSource = new BufferedStream(dwnlReq.GetResponse().GetResponseStream());
                BufferedStream dwnlTarget = new BufferedStream(new FileStream(chunkFile, FileMode.Append, FileAccess.Write));
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