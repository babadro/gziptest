namespace GZipTest
{
    public class Job
    {
        public Job(WorkType wType, byte[] data, long blockId = -1, long origBlockSize = 0)
        {
            WorkType = wType;
            Data = data;
            BlockId = blockId;
            OriginalBlockSize = origBlockSize;
        }

        public WorkType WorkType { get; set; }
        public byte[] Data { get; set; }
        public long BlockId { get; set; }
        public long OriginalBlockSize { get; set; }

        public override string ToString()
        {
            return $"BlockID: {BlockId}, WorkType: {WorkType}, DataLen: {Data.Length}, OriginalBlockSize: {OriginalBlockSize}";
        }
    }

    public enum WorkType: byte
    {
        Header = 0,
        FileLength = 1,
        Compress = 2,
        Decompress = 3,
    }
}
