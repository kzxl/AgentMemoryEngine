using System.IO.Compression;
using System.Text;
using AgentMemoryEngine.Core.BinaryLayout;

namespace AgentMemoryEngine.Core.Payload;

/// <summary>
/// Manages compression, decompression, and checksums for text/markdown and JSON payloads.
/// </summary>
public static class PayloadManager
{
    /// <summary>
    /// Compresses a string payload into a byte array with Deflate compression.
    /// </summary>
    public static byte[] CompressString(string text, out AmePayloadHeader header)
    {
        byte[] uncompressedBytes = Encoding.UTF8.GetBytes(text);
        uint checksum = ComputeCrc32(uncompressedBytes);

        using var memoryStream = new MemoryStream();
        using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflateStream.Write(uncompressedBytes);
        }

        byte[] compressedBytes = memoryStream.ToArray();

        header = new AmePayloadHeader
        {
            UncompressedSize = (uint)uncompressedBytes.Length,
            CompressedSize = (uint)compressedBytes.Length,
            ChunkChecksum = checksum,
            CompressionCodec = 2, // Deflate
            MimeType = 1,          // Text/Markdown
            Reserved = 0
        };

        return compressedBytes;
    }

    /// <summary>
    /// Decompresses a byte buffer back into a UTF-8 string according to payload header.
    /// </summary>
    public static string DecompressString(ReadOnlySpan<byte> compressedData, in AmePayloadHeader header)
    {
        if (header.CompressionCodec == 0) // Raw uncompressed
        {
            return Encoding.UTF8.GetString(compressedData[..(int)header.UncompressedSize]);
        }

        using var inputStream = new MemoryStream(compressedData[..(int)header.CompressedSize].ToArray());
        using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream((int)header.UncompressedSize);

        deflateStream.CopyTo(outputStream);
        byte[] decompressedBytes = outputStream.ToArray();

        uint actualChecksum = ComputeCrc32(decompressedBytes);
        if (actualChecksum != header.ChunkChecksum)
        {
            throw new InvalidDataException($"Payload checksum mismatch: expected {header.ChunkChecksum}, actual {actualChecksum}");
        }

        return Encoding.UTF8.GetString(decompressedBytes);
    }

    /// <summary>
    /// Computes a standard CRC32 checksum for data validation.
    /// </summary>
    public static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 & (uint)-(int)(crc & 1));
            }
        }
        return ~crc;
    }
}
