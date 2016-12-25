using System;
using System.IO;
using System.Net;

namespace Downloader
{
    /// <summary>
    /// defines all the chunks of a download job
    /// </summary>
    public class Chunks : IChunks
    {
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
        public Chunks(string chunkSource, long chunkSize, long totalSize)
        {
            //set chunk meta-data
            ChunkSize = chunkSize;
            TotalSize = totalSize;
            ChunkSource = chunkSource;
            ChunkCount = FindChunkCount();
            ChunkSize = ChunkCount != 1 ? chunkSize : totalSize;
            ChunkTargetTemplate = (uint)(ChunkSource + ChunkSize).GetHashCode() + "/file {0}.chunk";

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
            rangeReq.AddRange(0, ChunkSize);
            rangeReq.AllowAutoRedirect = true;

            //returns appropriate number of chunks
            using (HttpWebResponse rangeRes = (HttpWebResponse)rangeReq.GetResponse())
            {
                if (rangeRes.StatusCode < HttpStatusCode.Redirect && rangeRes.Headers[HttpResponseHeader.AcceptRanges] == "bytes")
                {
                    return (TotalSize / ChunkSize + (TotalSize % ChunkSize > 0 ? 1 : 0));
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
        /// getter for the chunk's start position
        /// </summary>
        /// <param name="id">chunk's id</param>
        /// <returns>start position of the chunk</returns>
        public long ChunkStart(long id) { return ChunkSize * id; }

        /// <summary>
        /// getter for the chunk's end position
        /// </summary>
        /// <param name="id">chunk's id</param>
        /// <returns>end position of the chunk</returns>
        public long ChunkEnd(long id) { return Math.Min(ChunkSize * id + ChunkSize - 1, TotalSize); }
    }
}
