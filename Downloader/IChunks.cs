namespace Downloader
{
    public interface IChunks
    {
        long ChunkCount { get; }
        string ChunkSource { get; }
        string ChunkTargetTemplate { get; }

        long ChunkEnd(long id);
        long ChunkStart(long id);
        string ChunkTarget(long id);
    }
}