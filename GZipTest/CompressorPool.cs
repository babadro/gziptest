using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GZipTest
{
    public class CompressorPool
    {
        private volatile bool _stop;
        private volatile bool _success;

        private readonly BlockingCollection<Job> _writer;
        private readonly BlockingCollection<Job> _reader;
        private readonly ConcurrentQueue<ConcurrentQueue<Job>> _pool;
        private readonly Compressor[] _compressors;
        private readonly Thread[] _compressorThreads;

        public CompressorPool(ushort compressorCount, BlockingCollection<Job> reader, BlockingCollection<Job> writer)
        {
            _writer = writer;
            _reader = reader;
            _pool = new ConcurrentQueue<ConcurrentQueue<Job>>();
            _compressors = new Compressor[compressorCount];
            _compressorThreads = new Thread[compressorCount];
        }

        public void Start()
        {
            for (ushort i = 0; i < _compressors.Length; i++)
            {
                var compressor = new Compressor(i, _pool, _writer);
                _compressors[i] = compressor;

                var thread = new Thread(() => compressor.Start());
                thread.IsBackground = true;
                _compressorThreads[i] = thread;
                thread.Start();
            }

            while (true)
            {
                Job job = null;
                SpinWait.SpinUntil(() => _stop || _reader.TryTake(out job));
                if (_stop)
                    break;

                ConcurrentQueue<Job> compressor = null;
                SpinWait.SpinUntil(() => _stop || _pool.TryDequeue(out compressor));
                if (_stop)
                    break;

                compressor.Enqueue(job);
            }

            foreach (var compressor in _compressors)
                compressor.Stop();
            foreach (var thread in _compressorThreads)
                thread.Join();

            Console.WriteLine("Compressor pool terminated");
            if (_success)
                Console.WriteLine("\nSuccess!\n");
            else
                Console.WriteLine("\nFailed!\n");
        }

        public void Stop(bool success = false)
        {
            _stop = true;
            _success = success;
        }
    }
}
