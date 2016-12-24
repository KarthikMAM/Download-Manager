using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace DownloadHelper
{
    /// <summary>
    /// downloader engine downloads the file in chunks
    /// </summary>
    public class Downloader
    {
        //constants
        private const string FILE_SIZE_SERVER = "http://proxyfilesize.appspot.com/index.php?url={0}";
        public const long MB = 1024 * 1024;

        //download file properties
        public string DwnlSource { private set; get; }
        public string DwnlTarget { set; get; }
        public long DwnlSize { private set; get; }

        //generalised chunk properties
        public string ChunkSource { get { return DwnlSource; } }
        public string ChunkTarget { get { return (uint)(DwnlSource + CHUNK_SIZE_LIMIT).GetHashCode() + "/file {0}.chunk"; } }
        public const long CHUNK_SIZE_LIMIT = 4 * MB;
        public long ChunkCount { private set; get; }

        //chunk's downloaders, trackers and data
        public const long ACTIVE_THREAD_LIMIT = 8;
        public DownloadTracker DwnlTracker;
        public ChunkDownloader[] ChunkDownloaders;
        public Exception DwnlException;

        //downloader state
        public enum DownloadState { Create, Start, Download, Append, Abort, Complete, Error, Finish, Idle }
        public DownloadState State { private set; get; }

        /// <summary>
        /// creates a downloader object and set basic params
        /// </summary>
        /// <param name="dwnlSource">where to download from</param>
        /// <param name="dwnlTarget">where to download to</param>
        public Downloader(string dwnlSource, String dwnlTarget)
        {
            //optimisations before download
            ServicePointManager.DefaultConnectionLimit = 500;
            ServicePointManager.Expect100Continue = false;

            //set download parameters
            DwnlSource = dwnlSource;
            DwnlTarget = dwnlTarget;

            //start the tracker
            DwnlTracker = new DownloadTracker(this);
            DwnlTracker.Start();

            //create the download
            Create();
        }

        /// <summary>
        /// creates the new download
        /// </summary>
        public void Create()
        {
            State = DownloadState.Create;

            try
            {
                //set the download parameters
                DwnlSize = FindFileSize();
                ChunkCount = FindChunkCount();

                //create a temp directory for chunks
                if (!Directory.Exists(Path.GetDirectoryName(ChunkTarget)))
                    Directory.CreateDirectory(Path.GetDirectoryName(ChunkTarget));

                //idle when no job is there
                State = DownloadState.Idle;
            }
            catch (Exception e)
            {
                //save the error
                DwnlException = e;
                State = DownloadState.Error;
            }
        }

        /// <summary>
        /// finds the allowed number of chunks
        /// </summary>
        /// <returns>the allowed chunk count</returns>
        private long FindChunkCount()
        {
            //request for finding the number of chunks
            HttpWebRequest rangeReq = WebRequest.CreateHttp(DwnlSource);
            rangeReq.AddRange(0, CHUNK_SIZE_LIMIT);
            rangeReq.AllowAutoRedirect = true;

            //returns appropriate number of chunks
            using (HttpWebResponse rangeRes = (HttpWebResponse)rangeReq.GetResponse())
            {
                if (rangeRes.StatusCode < HttpStatusCode.Redirect && rangeRes.Headers[HttpResponseHeader.AcceptRanges] == "bytes")
                {
                    return (DwnlSize / CHUNK_SIZE_LIMIT + (DwnlSize % CHUNK_SIZE_LIMIT > 0 ? 1 : 0));
                }
                else
                {
                    return 1;
                }
            }
        }

        /// <summary>
        /// finds the download file name from the url
        /// </summary>
        /// <param name="dwnlSource">the file whose name we want to find</param>
        /// <returns>the string with the file name</returns>
        public static string FindFileName(string dwnlSource)
        {
            //prepare the request headers
            HttpWebRequest fileNameReq = WebRequest.CreateHttp(dwnlSource);
            fileNameReq.AllowAutoRedirect = true;
            fileNameReq.AddRange(0, 10);

            using (HttpWebResponse fileNameRes = (HttpWebResponse)fileNameReq.GetResponse())
            {
                //get file name data headers
                string contentType = fileNameRes.ContentType;
                string physicalPath = fileNameRes.ResponseUri.AbsolutePath.Split('/').Last();

                //otherwise use the second part of the content-type header
                if (physicalPath.Contains('.'))
                {
                    return physicalPath;
                }
                else
                {
                    return physicalPath + "." + contentType.Split('/').Last();
                }
            }
        }

        /// <summary>
        /// finds the download file size from the url
        /// </summary>
        /// <returns>the download file size in bytes</returns>
        private long FindFileSize()
        {
            //first check if native algorithm works
            HttpWebRequest fileSizeReq = WebRequest.CreateHttp(DwnlSource);
            fileSizeReq.AllowAutoRedirect = true;

            using (HttpWebResponse fileSizeRes = (HttpWebResponse)fileSizeReq.GetResponse())
            {
                if (fileSizeRes.StatusCode < HttpStatusCode.Found)
                {
                    return fileSizeRes.ContentLength;
                }
                else
                {
                    //if problem use the proxy file size server
                    HttpWebRequest proxyFileSizeReq = WebRequest.CreateHttp(string.Format(FILE_SIZE_SERVER, DwnlSource));
                    using (HttpWebResponse proxyFileSizeRes = (HttpWebResponse)fileSizeReq.GetResponse())
                    using (StreamReader fileSizeReader = new StreamReader(proxyFileSizeRes.GetResponseStream()))
                        return long.Parse(fileSizeReader.ReadLine());
                }
            }
        }

        /// <summary>
        /// starts the entire process
        /// </summary>
        public void Start()
        {
            if (State == DownloadState.Idle)
            {
                State = DownloadState.Start;

                //creates the downloader thread's containers
                ChunkDownloaders = new ChunkDownloader[ChunkCount];
                for (long i = 0, chunkStart = 0; i < ChunkCount; i++, chunkStart += CHUNK_SIZE_LIMIT)
                {
                    ChunkDownloaders[i] = new ChunkDownloader(
                        ChunkSource,
                        String.Format(ChunkTarget, i),
                        chunkStart,
                        Math.Min(chunkStart + CHUNK_SIZE_LIMIT - 1, DwnlSize));
                }
                GC.Collect();

                //to prevent blocking threads used
                new Thread(() => { Download(); }).Start();
            }
        }

        /// <summary>
        /// starts and maintains the chunk downloader threads
        /// </summary>
        public void Download()
        {
            if (State == DownloadState.Start)
            {
                State = DownloadState.Download;

                //start allowed number of chunk downloaders
                long nextChunk;
                for (nextChunk = 0; nextChunk < Math.Min(ChunkCount, ACTIVE_THREAD_LIMIT); nextChunk++)
                {
                    ChunkDownloaders[nextChunk].Start();
                }

                //wait for the threads to complete
                //start the successive threads if not aborted
                for (int i = 0; i < ChunkCount; i++, nextChunk++)
                {
                    ChunkDownloaders[i].Join();

                    if (State == DownloadState.Abort) return;
                    else if (ChunkDownloaders[i].DwnlException != null)
                    {
                        Abort().Join();
                        DwnlException = ChunkDownloaders[i].DwnlException;
                        State = DownloadState.Error;
                        return;
                    }
                    else if (nextChunk < ChunkCount)
                    {
                        ChunkDownloaders[nextChunk].Start();
                    }
                }

                Append();
            }
        }

        /// <summary>
        /// appends the contents of the chunks to the target file
        /// </summary>
        public void Append()
        {
            State = DownloadState.Append;

            //synchronus copy of chunks to target file
            using (BufferedStream TargetFile = new BufferedStream(new FileStream(DwnlTarget, FileMode.Create, FileAccess.Write)))
                for (int i = 0; i < ChunkCount; i++)
                    using (BufferedStream SourceChunks = new BufferedStream(File.OpenRead(String.Format(ChunkTarget, i))))
                        SourceChunks.CopyTo(TargetFile);

            Complete();
        }

        /// <summary>
        /// actions after download completion
        /// </summary>
        public void Complete()
        {
            State = DownloadState.Complete;

            //cleanup job 1: delete the chunks
            for (int i = 0; i < ChunkCount; i++)
                File.Delete(String.Format(ChunkTarget, i));

            //cleanup job 2: delete the chunk directory
            Directory.Delete(Path.GetDirectoryName(ChunkTarget));

            State = DownloadState.Finish;
        }

        /// <summary>
        /// aborts the running download
        /// </summary>
        /// <returns>returns the abort thread</returns>
        public Thread Abort()
        {
            //abort each thread and jump to idle mode
            Thread abortThread = new Thread(() =>
            {
                if (State == DownloadState.Download)
                {
                    State = DownloadState.Abort;

                    //abort all the downloads
                    for (int i = 0; i < ChunkCount; i++)
                    {
                        ChunkDownloaders[i].Abort();
                    }

                    State = DownloadState.Idle;
                }
            });

            //return so that some can wait till abort is complete
            abortThread.Start();
            return abortThread;
        }
    }
}
