using System;
using System.Threading;

namespace Downloader
{
    public class DownloadScheduler : IDownloadScheduler
    {
        //constants
        private const long SCHEDULER_LIMIT = 10;

        //scheduler data
        private long nextChunk;
        private Thread[] schedulerThreads;

        //chunk download jobs and exception
        public Exception Error { private set; get; }
        public Chunks Chunks { private set; get; }

        /// <summary>
        /// initialize the download scheduler
        /// </summary>
        /// <param name="chunks">the chunk download jobs</param>
        public DownloadScheduler(Chunks chunks) { Chunks = chunks; }

        /// <summary>
        /// start the scheduler
        /// </summary>
        public void Start()
        {
            //little clean-up
            Error = null;
            nextChunk = 0;

            //create new scheduler threads
            schedulerThreads = new Thread[Math.Min(SCHEDULER_LIMIT, Chunks.ChunkCount)];
            for (int i = 0; i < schedulerThreads.Length; i++)
                schedulerThreads[i] = new Thread(() => Schedule());

            //start all the scheduler threads
            for (int i = 0; i < schedulerThreads.Length; i++) schedulerThreads[i].Start();

            //wait for the scheduler threads to finish
            for (int i = 0; i < schedulerThreads.Length; i++)
            {
                if (schedulerThreads[i].IsAlive)
                {
                    schedulerThreads[i].Join();
                }

                if (Error != null)
                {
                    throw Error;
                }
            }
        }

        /// <summary>
        /// aborts the scheduler threads and waits for them
        /// </summary>
        public void Abort()
        {
            //abort all the threads
            for (int i = 0; i < schedulerThreads.Length; i++)
            {
                if (schedulerThreads[i].IsAlive)
                {
                    schedulerThreads[i].Abort();
                }
            }

            //wait for all the threads to abort
            for (int i = 0; i < schedulerThreads.Length; i++)
            {
                if (schedulerThreads[i].IsAlive)
                {
                    schedulerThreads[i].Join();
                }
            }
        }

        /// <summary>
        /// scheduler thread logic
        /// </summary>
        private void Schedule()
        {
            try
            {
                while (true)
                {
                    long currentChunk = -1;
                    lock (Chunks)
                    {
                        //if next chunk available go to it
                        if (nextChunk < Chunks.ChunkCount)
                        {
                            currentChunk = nextChunk++;
                        }
                    }

                    //if ok download the next chunk
                    if (currentChunk != -1 && Error == null)
                    {
                        Chunks.DownloadChunk(currentChunk);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Error = e;
            }
        }
    }
}
