using System.IO;
using System.Linq;
using System.Net;

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
        public long CHUNK_SIZE_LIMIT = 4 * MB;

        //download file properties
        public string DwnlSource { private set; get; }
        public string DwnlTarget { set; get; }
        public long DwnlSize { private set; get; }
        public Chunks DwnlChunks { private set; get; }

        //download tracking properties
        public double DwnlSizeCompleted;
        public double DwnlSpeed;
        public double DwnlProgress { get { return DwnlSizeCompleted / DwnlSize * 100; } }

        //download append tracking properties
        public double AppendProgress
        {
            get
            {
                if (File.Exists(DwnlTarget))
                {
                    return (double)new FileInfo(DwnlTarget).Length / DwnlSize * 100;
                }
                else
                {
                    return 0;
                }
            }
        }

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

            //set the chunk data
            DwnlChunks = new Chunks(DwnlSource, CHUNK_SIZE_LIMIT, DwnlSize);
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
        /// finds the download file size from the url
        /// </summary>
        /// <returns>the download file size in bytes</returns>
        private static long FindFileSize(string dwnlSource)
        {
            //first check if native algorithm works
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
