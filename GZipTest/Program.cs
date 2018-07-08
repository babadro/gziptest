using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace GZipTest
{
    class Program
    {
        private const long BlockSize = 1048576; // 1 megabyte

        static void Main(string[] args)
        {
            var errMessage = CheckArguments(args);
            if (errMessage != null)
            {
                Console.WriteLine(errMessage);
                return;
            }

            #region Command line arguments

            var mode = args[0];
            var sourceFile = args[1];
            var destinationFile = args[2];
            ushort compressorsCount = (ushort) Environment.ProcessorCount;
            if (args.Length == 4)
                ushort.TryParse(args[3], out compressorsCount);

            #endregion

            var reader = new Reader(BlockSize);
            var writer = new Writer(BlockSize);
            var poolInput = new BlockingCollection<Job>(10);
            var writerInput = new BlockingCollection<Job>(10);
            var compressorPool = new CompressorPool(compressorsCount, poolInput, writerInput);

            var writerThread = new Thread(() => writer.Start(destinationFile, writerInput, compressorPool, reader));
            var compressorPoolThread = new Thread(() => compressorPool.Start());
            writerThread.IsBackground = true;
            compressorPoolThread.IsBackground = true;
            writerThread.Start();
            compressorPoolThread.Start();

            if (mode == "compress")
                reader.ProcessOriginalFile(sourceFile, writerInput, poolInput, writer, compressorPool);
            else // "decompress"
                reader.ProcessCompressedFile(sourceFile, writerInput, poolInput, writer, compressorPool);

            writerThread.Join();
            compressorPoolThread.Join();

            return;
        }

        private static string CheckArguments(string[] args)
        {
            if (args.Length < 3)
                return "Wrong number of arguments";

            if (args[0] != "compress" && args[0] != "decompress")
                return "First argument must be 'compress' or 'decompress'";

            var sourceFileName = args[1];
            if (sourceFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return $"{sourceFileName} is invalid file name";
            if (!File.Exists(sourceFileName))
                return $"{sourceFileName} file doesn't exist";

            var destinationFileName = args[2];
            if (destinationFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return $"{destinationFileName} is invalid file name";

            #region Threads
            if (args.Length > 3)
            {
                if (args.Length != 4)
                    return "Wrong number of arguments";

                ushort numOfThreads = 0;
                if (!ushort.TryParse(args[3], out numOfThreads) || numOfThreads == 0)
                    return "Number of threads argument must be a positive number";

            }

            return null;
            #endregion
        }

    }
}
