using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GZipTest
{
    public class Reader
    {
        private volatile bool _stop;

        private readonly long _blockSize;
        private const long FileMaxSize = 34359738368; // 32 GB

        public Reader(long blockSize)
        {
            _blockSize = blockSize;
        }

        public void ProcessOriginalFile(string fileName, BlockingCollection<Job> writerInput, BlockingCollection<Job> compressorsInput, Writer writer, CompressorPool compressors)
        {
            using (var streamSource = File.OpenRead(fileName))
            {
                long fileLength = streamSource.Length;
                if (fileLength > FileMaxSize)
                {
                    Console.WriteLine("File is too big to be compressed by this program. Max size is 32GB.");
                    writer.Stop();
                    compressors.Stop();
                    return;
                }
                byte[] size = BitConverter.GetBytes(fileLength);

                var headerJob = new Job(WorkType.Header, size);
                SpinWait.SpinUntil(() =>_stop || writerInput.TryAdd(headerJob));
                if (_stop)
                    return;

                var blockSize = fileLength < _blockSize ? fileLength : _blockSize; 
                long blockId = 0;
                while (fileLength > 0)
                {
                    byte[] data = new byte[blockSize];
                    streamSource.Read(data, 0, data.Length);

                    var compressJob = new Job(WorkType.Compress, data, blockId, blockSize);
                    SpinWait.SpinUntil(() =>_stop || compressorsInput.TryAdd(compressJob));
                    if (_stop)
                        break;

                    fileLength -= blockSize;

                    if (fileLength <= blockSize)
                        blockSize = fileLength;

                    blockId++;
                }
            }
        }

        public void ProcessCompressedFile(string fileName, BlockingCollection<Job> writerInput, BlockingCollection<Job> compressorsInput, Writer writer, CompressorPool compressors)
        {
            using (var streamSource = File.OpenRead(fileName))
            {
                var size = new byte[sizeof(long)];
                streamSource.Read(size, 0, size.Length);
                long origFileLength = BitConverter.ToInt64(size, 0);
                if (origFileLength > FileMaxSize || origFileLength < 0)
                {
                    Console.WriteLine("Error: wrong compressed file format.");
                    writer.Stop();
                    compressors.Stop();
                    return;
                }
                var fileLengthJob = new Job(WorkType.FileLength, size);
                SpinWait.SpinUntil(() =>_stop || writerInput.TryAdd(fileLengthJob));
                if (_stop)
                    return;

                long blockId;
                long origBlockSize = _blockSize;
                int compressedBlockSize;
                long workingSet = Process.GetCurrentProcess().WorkingSet64;

                while (origFileLength > 0)
                {
                    var id = new byte[sizeof(long)];
                    streamSource.Read(id, 0, id.Length);
                    blockId = BitConverter.ToInt16(id, 0);

                    size = new byte[sizeof(long)];
                    streamSource.Read(size, 0, size.Length);
                    origBlockSize = BitConverter.ToInt64(size, 0);

                    if (origBlockSize > workingSet || origBlockSize > origFileLength)
                    {
                        Console.WriteLine($"Compressed file reading error. Original block size is larger than available memory or original block size is larger than original file length");
                        Console.WriteLine($"Original block size: {origBlockSize}, workingSet: {workingSet}, block id: {blockId}, original file length: {origFileLength}");
                        compressors.Stop();
                        writer.Stop();
                        break;
                    }                        

                    size = new byte[sizeof(int)];
                    streamSource.Read(size, 0, size.Length);
                    compressedBlockSize = BitConverter.ToInt32(size, 0);
                    if (compressedBlockSize > workingSet)
                    {
                        Console.WriteLine($"Compressed file reading error. CompressedBlockSize ({compressedBlockSize}) > workingSet ({workingSet})");
                        compressors.Stop();
                        writer.Stop();
                        break;
                    }
                        

                    var compressedBlock = new byte[compressedBlockSize];
                    streamSource.Read(compressedBlock, 0, compressedBlock.Length);

                    var job = new Job(WorkType.Decompress, compressedBlock, blockId, origBlockSize);
                    SpinWait.SpinUntil(() =>_stop || compressorsInput.TryAdd(job));
                    if (_stop)
                        break;

                    origFileLength -= origBlockSize;

                    if (origFileLength <= origBlockSize)
                        origBlockSize = origFileLength;
                }
            }
        }

        public void Stop()
        {
            _stop = true;
        }
    }
}
