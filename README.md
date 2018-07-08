# gziptest
Multithreading compress and decompress file by chunks

Console application.
Example of usage in PowerShell:

Compress:
./GZipTest.exe compress sourcefile.txt sourcefile.txt.gz

Decompress:
./GZipTest.exe decompress sourcefile.txt.gz decompressed.txt

Консольное многопоточное приложение для поблочного сжатия и расжатия файлов размером до 32 гб.

Ревью не прошло, потому что:
1) Используются библиотеки из .net framework версии новее, чем 3.5
2) SpinWait здесь применять неэффективно
3) Работает только с относительными путями

