using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using NLog;


namespace AddTextChankToPngInZip
{
    /// <summary>
    /// Rename and add meta data to PNG files in zip file.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Default read buffer size.
        /// </summary>
        private const int DefaultBufferSize = 81920;
        /// <summary>
        /// Chunk type string of tEXt chunk.
        /// </summary>
        private const string ChunkNameText = "tEXt";
        /// <summary>
        /// Chunk type string of tIME chunk.
        /// </summary>
        private const string ChunkNameTime = "tIME";
        /// <summary>
        /// Chunk type string of IEND chunk.
        /// </summary>
        private const string ChunkNameIend = "IEND";
        /// <summary>
        /// Predefined keyword of tEXt chunk for short (one line) title or caption for image.
        /// </summary>
        private const string TextChunkKeyTitle = "Title";
        /// <summary>
        /// Predefined keyword of tEXt chunk for time of original image creation.
        /// </summary>
        private const string TextChunkKeyCreationTime = "Creation Time";

        /// <summary>
        /// <see cref="byte"/> array of PNG sinature.
        /// </summary>
        private static readonly byte[] PngSignature;
        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger;

        /// <summary>
        /// Initialize static members.
        /// </summary>
        static Program()
        {
            _logger = LogManager.GetCurrentClassLogger();
            PngSignature = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a };
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

        /// <summary>
        /// Process one zip file.
        /// </summary>
        /// <param name="srcZipFilePath">Source zip file path.</param>
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
                var tsDict = new Dictionary<DateTimeOffset, int>();

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
                        // if tsDict not contains srcEntry.LastWriteTime, n is initialized to 0.
                        tsDict.TryGetValue(srcEntry.LastWriteTime, out int n);
                        tsDict[srcEntry.LastWriteTime] = n + 1;

                        var entryParts = srcEntry.FullName.Split('/');
                        entryParts[^1] = $"cluster_{srcEntry.LastWriteTime:yyyy-MM-dd_HH-mm-ss}_{n:D3}.png";

                        var dstEntry = dstArchive.CreateEntry(
                            string.Join('/', entryParts),
                            CompressionLevel.Optimal);
                        dstEntry.LastWriteTime = srcEntry.LastWriteTime;
                        using (var srcZs = srcEntry.Open())
                        using (var dstZs = dstEntry.Open())
                        {
                            AddTextTimeChunk(
                                srcZs,
                                dstZs,
                                new List<KeyValuePair<string, string>>()
                                {
                                    KeyValuePair.Create(TextChunkKeyTitle, srcEntry.Name),
                                    KeyValuePair.Create(TextChunkKeyCreationTime, srcEntry.LastWriteTime.ToString("yyyy:MM:dd HH:mm:ss")),
                                },
                                srcEntry.LastWriteTime.DateTime);
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
                        errorImageList.Add(srcEntry.FullName);
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
        /// Add tEXt and tIME chunk while copying PNG data stream.
        /// </summary>
        /// <param name="srcPngStream">Source PNG data stream.</param>
        /// <param name="dstPngStream">Destination stream.</param>
        /// <param name="textChunkKeyValues">Key/Value list for tEXt chunk.</param>
        /// <param name="dt"><see cref="DateTime"/> for tIME chunk.</param>
        static void AddTextTimeChunk(Stream srcPngStream, Stream dstPngStream, List<KeyValuePair<string, string>> textChunkKeyValues, DateTime dt)
        {
            var buffer = new byte[DefaultBufferSize];
            if (srcPngStream.Read(buffer, 0, PngSignature.Length) < PngSignature.Length)
            {
                throw new Exception("Source PNG file data is too small.");
            }

            if (!HasPngSignature(buffer))
            {
                throw new Exception($"Invalid PNG signature: {string.Join(',', buffer.Select(b => "0x" + ((int)b).ToString("X2")))}");
            }

            // Write PNG Signature
            dstPngStream.Write(PngSignature, 0, PngSignature.Length);

            // Read IHDR
            using var br = new BinaryReader(srcPngStream, Encoding.ASCII, true);
            using var bw = new BinaryWriter(dstPngStream, Encoding.ASCII, true);

            Span<byte> chunkTypeData = stackalloc byte[4];
            string chunkType;

            var hasTimeChunk = false;
            var keySet = new HashSet<string>();

            do
            {
                var dataLength = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());
                if (br.Read(chunkTypeData) < chunkTypeData.Length)
                {
                    throw new Exception("Failed to read chunk type.");
                }

                chunkType = Encoding.ASCII.GetString(chunkTypeData);

                if (chunkType == ChunkNameText)
                {
                    buffer = EnsureCapacity(buffer, (int)dataLength);
                    if (br.BaseStream.Read(buffer, 0, (int)dataLength) < dataLength)
                    {
                        throw new Exception("Failed to read tEXt chunk data.");
                    }
                    var nullCharPos = Array.IndexOf(buffer, (byte)0, 0, (int)dataLength);
                    if (nullCharPos != -1)
                    {
                        var key = Encoding.ASCII.GetString(buffer, 0, nullCharPos);
                        _logger.Debug("key found: {0}", key);
                        keySet.Add(key);
                    }

                    bw.Write(BinaryPrimitives.ReverseEndianness(dataLength));
                    bw.Write(chunkTypeData);
                    bw.BaseStream.Write(buffer, 0, (int)dataLength);
                    if (br.BaseStream.Read(buffer, 0, 4) < 4)
                    {
                        throw new Exception("Failed to read CRC.");
                    }
                    bw.BaseStream.Write(buffer, 0, 4);

                    continue;
                }

                if (chunkType == ChunkNameTime)
                {
                    hasTimeChunk = true;
                }
                else if (chunkType == ChunkNameIend)
                {
                    // Insert tEXt and tIME chunks before IEND.
                    WriteTextChunks(bw, textChunkKeyValues.Where(kv => !keySet.Contains(kv.Key)));
                    if (!hasTimeChunk)
                    {
                        WriteTimeChunk(bw, dt);
                    }
                }

                // Copy current chunk
                bw.Write(BinaryPrimitives.ReverseEndianness(dataLength));
                bw.Write(chunkTypeData);

                var remLength = (int)dataLength + 4;
                buffer = EnsureCapacity(buffer, remLength);
                if (br.BaseStream.Read(buffer, 0, remLength) < remLength)
                {
                    throw new Exception("Failed to read chunk data and CRC.");
                }

                bw.BaseStream.Write(buffer, 0, remLength);
            } while (chunkType != ChunkNameIend);
        }

        /// <summary>
        /// Write tEXt chunks.
        /// </summary>
        /// <param name="bw"><see cref="BinaryWriter"/> of destination PNG stream.</param>
        /// <param name="textChunkKeyValues">Key/Value list for tEXt chunk.</param>
        private static void WriteTextChunks(BinaryWriter bw, IEnumerable<KeyValuePair<string, string>> textChunkKeyValues)
        {
            foreach (var p in textChunkKeyValues)
            {
                WriteTextChunk(bw, p.Key, p.Value);
            }
        }

        /// <summary>
        /// Write tEXt chunk.
        /// </summary>
        /// <param name="bw"><see cref="BinaryWriter"/> of destination PNG stream.</param>
        /// <param name="key">Key of tEXt chunk.</param>
        /// <param name="value">Value of tEXt chunk.</param>
        private static void WriteTextChunk(BinaryWriter bw, string key, string value)
        {
            var keyData = Encoding.ASCII.GetBytes(key);
            var valueData = Encoding.ASCII.GetBytes(value);

            bw.Write(BinaryPrimitives.ReverseEndianness(keyData.Length + 1 + valueData.Length));

            var textChunkTypeData = Encoding.ASCII.GetBytes(ChunkNameText);
            bw.Write(textChunkTypeData);

            bw.Write(keyData);
            bw.Write((byte)0);
            bw.Write(valueData);

            var crc = Crc32Calculator.Update(textChunkTypeData);
            crc = Crc32Calculator.Update(keyData, crc);
            crc = Crc32Calculator.Update((byte)0, crc);
            crc = Crc32Calculator.Update(valueData, crc);

            bw.Write(BinaryPrimitives.ReverseEndianness(Crc32Calculator.Finalize(crc)));
        }

        /// <summary>
        /// Write tIME chunk.
        /// </summary>
        /// <param name="bw"><see cref="BinaryWriter"/> of destination PNG stream.</param>
        /// <param name="dt"><see cref="DateTime"/> value for tIME chunk.</param>
        private static void WriteTimeChunk(BinaryWriter bw, DateTime dt)
        {
            Span<byte> dtData = stackalloc byte[] {
                (byte)((dt.Year & 0xff00) >> 8),
                (byte)(dt.Year & 0xff),
                (byte)dt.Month,
                (byte)dt.Day,
                (byte)dt.Hour,
                (byte)dt.Minute,
                (byte)dt.Second,
            };

            bw.Write(BinaryPrimitives.ReverseEndianness(dtData.Length));

            var textChunkTypeData = Encoding.ASCII.GetBytes(ChunkNameTime);

            bw.Write(textChunkTypeData);
            bw.Write(dtData);

            var crc = Crc32Calculator.Update(textChunkTypeData);
            crc = Crc32Calculator.Update(dtData, crc);

            bw.Write(BinaryPrimitives.ReverseEndianness(Crc32Calculator.Finalize(crc)));
        }

        /// <summary>
        /// Identify the specified binary data has a PNG signature or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a PNG signature, otherwise false.</returns>
        private static bool HasPngSignature(byte[] data)
        {
            if (data.Length < PngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (data[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ensures that the capacity of <paramref name="data"/> is at least the specified value, <paramref name="required"/>.
        /// </summary>
        /// <param name="data">Souce <see cref="byte"/> array.</param>
        /// <param name="required">Required capacity</param>
        /// <returns><paramref name="data"/> if <c><paramref name="data"/>.Length &gt;= <paramref name="required"/></c>,
        /// otherwise new allocated <see cref="byte"/> array.</returns>
        private static byte[] EnsureCapacity(byte[] data, int required)
        {
            return data.Length < required ? new byte[required] : data;
        }
    }
}
