using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace GZipTest
{
    public class Writer
    {
        private volatile bool _stop;

        private readonly long _blockSize;

        public Writer(long blockSize)
        {
            _blockSize = blockSize;
        }

        public void Start(string fileName, BlockingCollection<Job> jobs, CompressorPool compressors, Reader reader)
        {
            var fileInfo = new FileInfo(fileName);
            if (IsFileLocked(fileInfo))
            {
                Console.WriteLine($"Error: file {fileName} is locked!\n");
                compressors.Stop();
                reader.Stop();
                return;
            }
            fileInfo.Delete();
            
            using (var stream = fileInfo.OpenWrite())
            {
                long origFileLength = long.MaxValue;
                while (origFileLength > 0)
                {
                    Job job = null;
                    SpinWait.SpinUntil(() => _stop || jobs.TryTake(out job));
                    if (_stop)
                        break;

                    switch (job.WorkType)
                    {
                        case WorkType.Header:
                            Write(stream, job.Data); // Запись заголовка с размером всего файла до сжатия
                            origFileLength = BitConverter.ToInt64(job.Data, 0);
                            break;
                        case WorkType.FileLength:
                            origFileLength = BitConverter.ToInt64(job.Data, 0);
                            break;
                        case WorkType.Compress:
                            // Записать ид куска
                            var id = BitConverter.GetBytes(job.BlockId);
                            Write(stream, id);

                            // Записать размер куска до сжатия
                            var origBlockSize = BitConverter.GetBytes(job.OriginalBlockSize);
                            Write(stream, origBlockSize);

                            // Записать размер сжатого куска
                            var compressedBlockSize = BitConverter.GetBytes(job.Data.Length);
                            Write(stream, compressedBlockSize);

                            // Записать блок
                            Write(stream, job.Data);
                            break;
                        case WorkType.Decompress:
                            stream.Position = job.BlockId * _blockSize;
                            Write(stream, job.Data);
                            break;

                        default:
                            throw new Exception("Unknown work type in writer");
                    }

                    origFileLength -= job.OriginalBlockSize;
                    Console.WriteLine(job);
                };
                compressors.Stop(success: origFileLength == 0);
            }
        }

        private void Write(FileStream stream, byte[] data)
        {
            stream.Write(data, 0, data.Length);
        }

        private bool IsFileLocked(FileInfo file)
        {
            if (!file.Exists)
                return false;

            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                stream?.Close();
            }
            return false;
        }

        public void Stop()
        {
            _stop = true;
        }
    }
}
