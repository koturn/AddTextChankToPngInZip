using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Media.Imaging;

using NLog;


namespace AddTextChankToPngInZip
{
    /// <summary>
    /// Rename and add meta data to PNG files in zip file.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger;

        /// <summary>
        /// Initialize static members.
        /// </summary>
        static Program()
        {
            SetupConsole();
            _logger = LogManager.GetCurrentClassLogger();
        }


        /// <summary>
        /// An entry point of this program.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Status code.</returns>
        static int Main(string[] args)
        {
            if (args.Length == 0) {
                Console.Error.WriteLine("Number of arguments must be one or more");
                return 1;
            }

            foreach (var arg in args)
            {
                AddTextChunkToPngInZip(arg);
            }

            _logger.Info("All work DONE!");

            return 0;
        }

        static void AddTextChunkToPngInZip(string srcZipFilePath)
        {
            _logger.Info("Target zip file: {0}", srcZipFilePath);
            var tmpZipFilePath = Path.Combine(
                Path.GetDirectoryName(srcZipFilePath),
                Path.GetFileNameWithoutExtension(srcZipFilePath) + ".tmp.zip");

            if (File.Exists(tmpZipFilePath))
            {
                File.Delete(tmpZipFilePath);
            }

            var errorImageList = new List<string>();

            using (var srcArchive = ZipFile.OpenRead(srcZipFilePath))
            using (var dstArchive = ZipFile.Open(tmpZipFilePath, ZipArchiveMode.Create))
            {
                void CopyZipEntry(ZipArchiveEntry srcEntry, int procIndex, Stopwatch sw)
                {
                    try
                    {
                        using (var srcZs = srcEntry.Open())
                        {
                            var dstEntry = dstArchive.CreateEntry(srcEntry.FullName, CompressionLevel.Optimal);
                            dstEntry.LastWriteTime = srcEntry.LastWriteTime;

                            using var dstZs = dstEntry.Open();
                            srcZs.CopyTo(dstZs);
                        }

                        _logger.Info(
                            "[{0}] Copy {1} done: {2:F3} seconds",
                            procIndex,
                            srcEntry.FullName,
                            sw.ElapsedMilliseconds / 1000.0);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[{0}] Copy {1} failed: ", procIndex, srcEntry.FullName);
                    }
                }

                var nProcPngFiles = 0;

                foreach (var srcEntry in srcArchive.Entries)
                {
                    _logger.Info("{0}", srcEntry.FullName);

                    var sw = Stopwatch.StartNew();
                    var procIndex = nProcPngFiles;
                    nProcPngFiles++;

                    if (!srcEntry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyZipEntry(srcEntry, procIndex, sw);
                        continue;
                    }

                    try
                    {
                        using var srcZs = srcEntry.Open();

                        var dec = new PngBitmapDecoder(srcZs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                        var frame = BitmapFrame.Create(dec.Frames[0]);

                        var meta = (BitmapMetadata)frame.Metadata;
                        meta.SetQuery("/[0]tEXt/Title", srcEntry.Name);

                        // For the Creation Time keyword, the date format defined in section 5.2.14 of RFC 1123 is suggested
                        // but file explorer of Windows detects following format, "yyyy:MM:dd HH:mm.ss".
                        //   meta.SetQuery("/[1]tEXt/Creation Time", srcEntry.LastWriteTime.ToString("r"));
                        meta.SetQuery("/[1]tEXt/Creation Time", srcEntry.LastWriteTime.ToString("yyyy:MM:dd HH:mm:ss"));

                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(frame);

                        using var ms = new MemoryStream();
                        enc.Save(ms);

                        var entryParts = srcEntry.FullName.Split('/');

                        var prefix = string.Join('/', entryParts.Take(entryParts.Length - 1));
                        if (prefix.Length > 0) {
                            prefix += "/";
                        }
                        var dstEntry = dstArchive.CreateEntry(
                            prefix + "cluster_" + srcEntry.LastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".png",
                            CompressionLevel.Optimal);
                        dstEntry.LastWriteTime = srcEntry.LastWriteTime;
                        using (var dstZs = dstEntry.Open())
                        {
                            // "ms.CopyTo(dstZs)" doesn't work well...
                            var bytes = ms.GetBuffer();
                            dstZs.Write(bytes, 0, (int)ms.Length);
                        }

                        _logger.Info("[{0}] {1} -> {2}", procIndex, srcEntry.FullName, dstEntry.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            ex,
                            "[{0}] Failed to export {1}:",
                            procIndex,
                            srcEntry.FullName);
                        CopyZipEntry(srcEntry, procIndex, sw);
                        lock (((ICollection)errorImageList).SyncRoot)
                        {
                            errorImageList.Add(srcEntry.FullName);
                        }
                    }
                };
            }

            // Rename original file
            File.Move(
                srcZipFilePath,
                Path.Combine(
                    Path.GetDirectoryName(srcZipFilePath),
                    Path.GetFileNameWithoutExtension(srcZipFilePath) + ".old.zip"),
                true);
            // Rename temporary file
            File.Move(tmpZipFilePath, srcZipFilePath);

            if (errorImageList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageList.Count);
                int cnt = 1;
                foreach (var fullname in errorImageList)
                {
                    Console.WriteLine($"Error image [{cnt}]: {fullname}");
                    cnt++;
                }
            }
        }

        /// <summary>
        /// Setup console to output stdout messages.
        /// </summary>
        /// <returns>true if </returns>
        static bool SetupConsole()
        {
            if (UnsafeNativeMethods.AttachConsole(-1))
            {
                return false;
            }

            if (!UnsafeNativeMethods.AllocConsole())
            {
                return false;
            }

            // Console.SetOut(new StreamWriter(Console.OpenStandardOutput())
            // {
            //     AutoFlush = true,
            // });

            return true;
        }

        /// <summary>
        /// P/Invoke functions.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal static class UnsafeNativeMethods
        {
            /// <summary>
            /// Attaches the calling process to the console of the specified process as a client application.
            /// </summary>
            /// <returns>If the function succeeds, the return value is <c>true</c>.</returns>
            [DllImport("Kernel32.dll")]
            public static extern bool AttachConsole(int processId);
            /// <summary>
            /// Allocates a new console for the calling process.
            /// </summary>
            /// <returns>If the function succeeds, the return value is <c>true</c>.</returns>
            [DllImport("Kernel32.dll")]
            public static extern bool AllocConsole();
            /// <summary>
            /// Detaches the calling process from its console.
            /// </summary>
            /// <returns>If the function succeeds, the return value is <c>true</c>.</returns>
            [DllImport("Kernel32.dll")]
            public static extern bool FreeConsole();
        }
    }
}
