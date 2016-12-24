using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DownloadHelper
{
    public class ChunkDownloader
    {
        //download thread
        private Thread dwnlThread;
        public Exception DwnlException { private set; get; }
        private const int BUFFER_SIZE = 8192;
        private byte[] buffer = new byte[BUFFER_SIZE];

        //chunk data
        private string chunkSource;
        private string chunkTarget;
        private long chunkStart;
        private long chunkEnd;

        /// <summary>
        /// initializes the download thread for the given chunk
        /// </summary>
        /// <param name="chunkSource">url to download from</param>
        /// <param name="chunkTarget">url to save download</param>
        /// <param name="chunkStart">starting position of the chunk</param>
        /// <param name="chunkEnd">ending position of the chunk</param>
        public ChunkDownloader(string chunkSource, string chunkTarget, long chunkStart, long chunkEnd)
        {
            //set the parameters
            this.chunkSource = chunkSource;
            this.chunkTarget = chunkTarget;
            this.chunkStart = chunkStart;
            this.chunkEnd = chunkEnd;

            //create a new thread to download the chunk
            //and abort if the dwnl request is still open
            dwnlThread = new Thread(new ThreadStart(Download));
        }

        /// <summary>
        /// start the thread if not running
        /// </summary>
        public void Start()
        {
            dwnlThread.Start();
        }

        /// <summary>
        /// waits till the thread finishes running
        /// </summary>
        public void Join()
        {
            if (dwnlThread.IsAlive)
            {
                dwnlThread.Join();
            }
        }

        /// <summary>
        /// abort thread the thread
        /// </summary>
        public void Abort()
        {
            if (dwnlThread.IsAlive)
            {
                dwnlThread.Abort();
                dwnlThread.Join();
            }
        }

        /// <summary>
        /// download logic
        /// </summary>
        public void Download()
        {
            //adjust the download range and the completed part
            chunkStart += File.Exists(chunkTarget) ? new FileInfo(chunkTarget).Length : 0;

            //check if there is a need to download
            if (chunkStart < chunkEnd)
            {
                //prepare the download request
                HttpWebRequest dwnlReq = WebRequest.CreateHttp(chunkSource);
                dwnlReq.AllowAutoRedirect = true;
                dwnlReq.AddRange(chunkStart, chunkEnd);
                dwnlReq.ServicePoint.ConnectionLimit = 100;
                dwnlReq.ServicePoint.Expect100Continue = false;

                try
                {
                    using (HttpWebResponse dwnlRes = (HttpWebResponse)dwnlReq.GetResponse())
                    using (Stream dwnlSource = dwnlRes.GetResponseStream())
                    using (FileStream dwnlTarget = new FileStream(chunkTarget, FileMode.Append, FileAccess.Write))
                    {
                        int bufferdSize;
                        do
                        {
                            //read the download response async
                            //in the mean time flush the writable data and wait for result
                            //write the new buffered data to the target stream
                            Task<int> bufferReader = dwnlSource.ReadAsync(buffer, 0, BUFFER_SIZE);
                            dwnlTarget.Flush();
                            bufferReader.Wait();
                            bufferdSize = bufferReader.Result;
                            dwnlTarget.Write(buffer, 0, bufferdSize);
                        } while (bufferdSize > 0);
                    }

                }
                catch (Exception e)
                {
                    DwnlException = e;
                }
                finally
                {
                    dwnlReq.Abort();
                }
            }
        }
    }
}
