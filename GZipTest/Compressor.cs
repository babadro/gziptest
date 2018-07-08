using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public class Compressor
    {
        private volatile bool _stop;

        private readonly ushort _number;
        private readonly ConcurrentQueue<ConcurrentQueue<Job>> _pool;
        private readonly BlockingCollection<Job> _writer;
        private readonly ConcurrentQueue<Job> _me;

        public Compressor(ushort number, ConcurrentQueue<ConcurrentQueue<Job>> pool, BlockingCollection<Job> writer)
        {
            _number = number;
            _pool = pool;
            _writer = writer;
            _me = new ConcurrentQueue<Job>();
        }

        public void Start()
        {
            while (true)
            {
                _pool.Enqueue(_me);
                Job job = null;
                SpinWait.SpinUntil(() => _stop || _me.TryDequeue(out job));
                if (_stop)
                    break;

                if (job.WorkType == WorkType.Compress)
                    Compress(job);
                else
                    Decompress(job);

                SpinWait.SpinUntil(() => _stop || _writer.TryAdd(job));
                if (_stop)
                    break;
            }
            Console.WriteLine($"Compressor #{_number + 1} terminated");
        }

        private void Compress(Job job)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var stream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    stream.Write(job.Data, 0, job.Data.Length);
                }

                job.Data = memoryStream.GetBuffer();
            }
        }

        private void Decompress(Job job)
        {
            using (var memoryStream = new MemoryStream(job.Data))
            {
                var decompressedBlock = new byte[job.OriginalBlockSize];
                using (var stream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    stream.Read(decompressedBlock, 0, decompressedBlock.Length);
                }

                job.Data = decompressedBlock;
            }
        }

        public void Stop()
        {
            _stop = true;
        }
    }    
}
