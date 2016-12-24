using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    /// <summary>
    /// each of these downloads one chunk of the download
    /// </summary>
    public class ChunkDownloader : IChunkDownloader
    {
        //constants
        private const int BUFFER_SIZE = 8192;

        //download thread and exceptions
        private Thread dwnlThread;
        private Exception dwnlException;

        //chunk data
        private Chunks chunks;
        private long id;

        /// <summary>
        /// initializes a downloader for the given chunk
        /// </summary>
        /// <param name="chunks">chunks meta data repo</param>
        /// <param name="id">id of the chunk</param>
        public ChunkDownloader(Chunks chunks, long id)
        {
            //set the parameters
            this.chunks = chunks;
            this.id = id;
        }

        /// <summary>
        /// create and start the download thread
        /// </summary>
        public void Start()
        {
            if (dwnlThread == null || !dwnlThread.IsAlive)
            {
                dwnlException = null;
                dwnlThread = new Thread(new ThreadStart(Download));
                dwnlThread.Start();
            }
        }

        /// <summary>
        /// waits till the thread finishes running
        /// if there is any error throws exceptions
        /// </summary>
        public void Join()
        {
            if (dwnlThread != null && dwnlThread.IsAlive)
            {
                dwnlThread.Join();
            }
            if (dwnlException != null) throw dwnlException;
        }

        /// <summary>
        /// aborts the running thread
        /// </summary>
        public void Abort()
        {
            if (dwnlThread != null && dwnlThread.IsAlive)
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
            long chunkStart = chunks.ChunkStart(id);
            long chunkEnd = chunks.ChunkEnd(id);
            chunkStart += Interlocked.Exchange(ref chunks.ChunkProgress[id],
                File.Exists(chunks.ChunkTarget(id)) ? new FileInfo(chunks.ChunkTarget(id)).Length : 0);

            //check if there is a need to download
            if (chunkStart < chunkEnd)
            {
                //prepare the download request
                HttpWebRequest dwnlReq = WebRequest.CreateHttp(chunks.ChunkSource);
                dwnlReq.AllowAutoRedirect = true;
                dwnlReq.AddRange(chunkStart, chunkEnd);
                dwnlReq.ServicePoint.ConnectionLimit = 100;
                dwnlReq.ServicePoint.Expect100Continue = false;

                try
                {
                    using (HttpWebResponse dwnlRes = (HttpWebResponse)dwnlReq.GetResponse())
                    using (Stream dwnlSource = dwnlRes.GetResponseStream())
                    using (FileStream dwnlTarget = new FileStream(chunks.ChunkTarget(id), FileMode.Append, FileAccess.Write))
                    {
                        //buffer and downloaded buffer size
                        int downloadedBufferSize;
                        byte[] buffer = new byte[BUFFER_SIZE];
                        do
                        {
                            //read the download response async
                            //in the mean time flush the writable data and wait for result
                            //write the new buffered data to the target stream
                            Task<int> bufferReader = dwnlSource.ReadAsync(buffer, 0, BUFFER_SIZE);
                            dwnlTarget.Flush();
                            bufferReader.Wait();
                            downloadedBufferSize = bufferReader.Result;
                            Interlocked.Add(ref chunks.ChunkProgress[id], downloadedBufferSize);
                            dwnlTarget.Write(buffer, 0, downloadedBufferSize);
                        } while (downloadedBufferSize > 0);
                    }

                }
                catch (ThreadAbortException) { /* ignore this exception */ }
                catch (Exception e)
                {
                    dwnlException = e;
                }
                finally
                {
                    dwnlReq.Abort();
                }
            }
        }
    }
}
