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
    public class ChunkDownloader
    {
        ////constants
        //private const int BUFFER_SIZE = 8192;

        ////download thread and exceptions
        //private Thread dwnlThread;
        //private Exception dwnlException;

        ////chunk data
        //private Chunks chunks;
        //private long id;

        ///// <summary>
        ///// initializes a downloader for the given chunk
        ///// </summary>
        ///// <param name="chunks">chunks meta data repo</param>
        ///// <param name="id">id of the chunk</param>
        //public ChunkDownloader(Chunks chunks, long id)
        //{
        //    //set the parameters
        //    this.chunks = chunks;
        //    this.id = id;
        //}

        ///// <summary>
        ///// create and start the download thread
        ///// </summary>
        //public void Start()
        //{
        //    dwnlException = null;
        //    dwnlThread = new Thread(() => Download());
        //    dwnlThread.Start();
        //}

        ///// <summary>
        ///// waits till the thread finishes running
        ///// if there is any error throws exceptions
        ///// </summary>
        //public void Join()
        //{
        //    if (dwnlThread != null && dwnlThread.IsAlive)
        //    {
        //        dwnlThread.Join();
        //    }
        //    if (dwnlException != null) throw dwnlException;
        //}

        ///// <summary>
        ///// aborts the running thread
        ///// </summary>
        //public void Abort()
        //{
        //    if (dwnlThread != null && dwnlThread.IsAlive)
        //    {
        //        dwnlThread.Abort();
        //        dwnlThread.Join();
        //    }
        //}

        
    }
}
