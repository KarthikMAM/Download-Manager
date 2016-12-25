using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace Downloader
{
    /// <summary>
    /// defines the download job
    /// </summary>
    public class Download : IDownload
    {
        //constants
        private const string FILE_SIZE_SERVER = "http://proxyfilesize.appspot.com/index.php?url={0}";
        public const long MB = 1024 * 1024;

        //download file properties
        public string DwnlSource { private set; get; }
        public string DwnlTarget { set; get; }
        public long DwnlSize { private set; get; }

        //download chunk jobs and scheduler
        public Chunks DwnlChunks { private set; get; }
        public DownloadScheduler DwnlScheduler { private set; get; }

        //download tracking properties
        public double DwnlSizeCompleted { private set; get; }
        public double DwnlSpeed { private set; get; }
        public double DwnlProgress { private set; get; }
        private long windowStart;

        //download append tracking properties
        public double AppendProgress { private set; get; }

        /// <summary>
        /// creates a new download job
        /// </summary>
        /// <param name="dwnlSource">url to download from</param>
        /// <param name="dwnlTarget">url to download to</param>
        public Download(string dwnlSource, string dwnlTarget)
        {
            //set the download data
            DwnlSource = dwnlSource;
            DwnlTarget = dwnlTarget;
            DwnlSize = FindFileSize(DwnlSource);

            //create the virtual chunk download jobs and its scheduler
            DwnlChunks = new Chunks(DwnlSource, DwnlSize);
            DwnlScheduler = new DownloadScheduler(DwnlChunks);
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
                //get file name markers in the data headers
                string contentType = fileNameRes.ContentType;
                string physicalPath = fileNameRes.ResponseUri.AbsolutePath.Split('/').Last();

                //use contenttype if extension not found
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
        /// updates the download progress
        /// </summary>
        /// <param name="timeSpan">time span of update</param>
        public void UpdateDownloadProgress(double timeSpan)
        {
            //initial download progress parameters
            double bufferedSize;
            double downloadedSize = DwnlChunks.ChunkSize * windowStart;
            Chunks chunks = DwnlChunks;

            //adjust the start of the window if completed
            while (windowStart < chunks.ChunkCount)
            {
                bufferedSize = Interlocked.Read(ref chunks.ChunkProgress[windowStart]);
                if (bufferedSize == chunks.ChunkSize)
                {
                    windowStart++;
                    downloadedSize += chunks.ChunkSize;
                }
                else
                {
                    break;
                }
            }

            //update the size of the active chunks
            for (long i = windowStart; i < chunks.ChunkCount; i++)
            {
                bufferedSize = Interlocked.Read(ref DwnlChunks.ChunkProgress[i]);
                if (bufferedSize != 0)
                {
                    downloadedSize += bufferedSize;
                }
                else
                {
                    break;
                }
            }

            //compute the speed and progress
            DwnlSpeed = Math.Max(0, downloadedSize - DwnlSizeCompleted) / timeSpan;
            DwnlSizeCompleted = downloadedSize;
            DwnlProgress = DwnlSizeCompleted / DwnlSize * 100;
        }

        /// <summary>
        /// updating the append progress
        /// </summary>
        public void UpdateAppendProgress()
        {
            AppendProgress = File.Exists(DwnlTarget) ? (double)new FileInfo(DwnlTarget).Length / DwnlSize * 100 : 0;
        }

        /// <summary>
        /// finds the download file size from the url
        /// </summary>
        /// <returns>the download file size in bytes</returns>
        private static long FindFileSize(string dwnlSource)
        {
            //first create a native header request
            HttpWebRequest fileSizeReq = WebRequest.CreateHttp(dwnlSource);
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
                    HttpWebRequest proxyFileSizeReq = WebRequest.CreateHttp(string.Format(FILE_SIZE_SERVER, dwnlSource));
                    using (HttpWebResponse proxyFileSizeRes = (HttpWebResponse)fileSizeReq.GetResponse())
                    using (StreamReader fileSizeReader = new StreamReader(proxyFileSizeRes.GetResponseStream()))
                        return long.Parse(fileSizeReader.ReadLine());
                }
            }
        }
    }
}
