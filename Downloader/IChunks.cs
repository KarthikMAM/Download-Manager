namespace Downloader
{
    public interface IChunks
    {
        long ChunkCount { get; }
        long[] ChunkProgress { get; }
        long ChunkSize { get; }
        string ChunkSource { get; }
        string ChunkTargetTemplate { get; }
        long TotalSize { get; }

        string ChunkTarget(long id);
        void Download(long id);
    }
}