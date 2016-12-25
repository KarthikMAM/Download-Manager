using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    /// <summary>
    /// defines all the chunk download jobs of a download job
    /// </summary>
    public class Chunks : IChunks
    {
        //constants
        public const int CHUNK_BUFFER_SIZE = 8192;
        public const long CHUNK_SIZE_LIMIT = 10 * 1024 * 1024;

        //chunk meta-data
        public long ChunkSize { private set; get; }
        public long ChunkCount { private set; get; }
        public string ChunkSource { private set; get; }
        public string ChunkTargetTemplate { private set; get; }

        //chunk tracking data
        public long[] ChunkProgress { private set; get; }
        public long TotalSize { private set; get; }

        /// <summary>
        /// creates the chunk data repo
        /// </summary>
        /// <param name="chunkSource">url to download the chunk from</param>
        /// <param name="chunkSize">maximum size of each chunk</param>
        /// <param name="totalSize">total size of all the chunk</param>
        public Chunks(string chunkSource, long totalSize)
        {
            //set chunk meta-data
            TotalSize = totalSize;
            ChunkSource = chunkSource;
            ChunkCount = FindChunkCount();
            ChunkSize = ChunkCount != 1 ? CHUNK_SIZE_LIMIT : totalSize;
            ChunkTargetTemplate = (uint)(ChunkSource + ChunkSize + TotalSize).GetHashCode() + "/file {0}.chunk";

            //set chunks tracking data
            ChunkProgress = new long[ChunkCount];

            //create a temp directory for chunks
            if (!Directory.Exists(Path.GetDirectoryName(ChunkTarget(0))))
                Directory.CreateDirectory(Path.GetDirectoryName(ChunkTarget(0)));
        }

        /// <summary>
        /// finds the allowed number of chunks
        /// </summary>
        /// <returns>the allowed chunk count</returns>
        private long FindChunkCount()
        {
            //request for finding the number of chunks
            HttpWebRequest rangeReq = WebRequest.CreateHttp(ChunkSource);
            rangeReq.AddRange(0, CHUNK_SIZE_LIMIT);
            rangeReq.AllowAutoRedirect = true;

            //returns appropriate number of chunks based on accept-ranges
            using (HttpWebResponse rangeRes = (HttpWebResponse)rangeReq.GetResponse())
            {
                if (rangeRes.StatusCode < HttpStatusCode.Redirect && rangeRes.Headers[HttpResponseHeader.AcceptRanges] == "bytes")
                {
                    return (TotalSize / CHUNK_SIZE_LIMIT + (TotalSize % CHUNK_SIZE_LIMIT > 0 ? 1 : 0));
                }
                else
                {
                    return 1;
                }
            }
        }

        /// <summary>
        /// getter for the chunk target based on chunk's id
        /// </summary>
        /// <param name="id">chunk's id</param>
        /// <returns>chunk's target path</returns>
        public string ChunkTarget(long id) { return string.Format(ChunkTargetTemplate, id); }

        /// <summary>
        /// chunk download logic
        /// </summary>
        public void Download(long id)
        {
            //adjust the download range and progress for resume connections
            long chunkStart = ChunkSize * id;
            long chunkEnd = Math.Min(chunkStart + ChunkSize - 1, TotalSize);
            long chunkDownloaded = File.Exists(ChunkTarget(id)) ? new FileInfo(ChunkTarget(id)).Length : 0;
            chunkStart += chunkDownloaded;
            ChunkProgress[id] = chunkDownloaded;

            //check if there is a need to download
            if (chunkStart < chunkEnd)
            {
                //prepare the download request
                HttpWebRequest dwnlReq = WebRequest.CreateHttp(ChunkSource);
                dwnlReq.AllowAutoRedirect = true;
                dwnlReq.AddRange(chunkStart, chunkEnd);
                dwnlReq.ServicePoint.ConnectionLimit = 100;
                dwnlReq.ServicePoint.Expect100Continue = false;

                try
                {
                    using (HttpWebResponse dwnlRes = (HttpWebResponse)dwnlReq.GetResponse())
                    using (Stream dwnlSource = dwnlRes.GetResponseStream())
                    using (FileStream dwnlTarget = new FileStream(ChunkTarget(id), FileMode.Append, FileAccess.Write))
                    {
                        //buffer and downloaded buffer size
                        int downloadedBufferSize;
                        byte[] buffer = new byte[CHUNK_BUFFER_SIZE];

                        do
                        {
                            //read the download response async
                            //in the mean time flush the writable data and wait for result
                            //write the new buffered data to the target stream

                            Task<int> bufferReader = dwnlSource.ReadAsync(buffer, 0, CHUNK_BUFFER_SIZE);
                            dwnlTarget.Flush();
                            bufferReader.Wait();
                            downloadedBufferSize = bufferReader.Result;
                            Interlocked.Add(ref ChunkProgress[id], downloadedBufferSize);
                            dwnlTarget.Write(buffer, 0, downloadedBufferSize);

                        } while (downloadedBufferSize > 0);
                    }

                }
                finally
                {
                    //abort if request is still open
                    dwnlReq.Abort();
                }
            }
        }
    }
}
