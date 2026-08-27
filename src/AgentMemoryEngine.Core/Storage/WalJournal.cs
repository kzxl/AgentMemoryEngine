using System.Runtime.InteropServices;
using AgentMemoryEngine.Core.Payload;

namespace AgentMemoryEngine.Core.Storage;

public enum AmeWalOpType : byte
{
    InsertRecord = 1,
    TouchCognitive = 2,
    AddGraphEdge = 3
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
public struct AmeWalRecordHeader
{
    public byte OpType;
    public byte Reserved;
    public ushort HeaderChecksum;
    public uint DataLength;
    public uint DataChecksum;
    public uint Timestamp;
}

/// <summary>
/// Append-only Write-Ahead Log (WAL) journal for crash recovery and atomic state persistence.
/// </summary>
public sealed class WalJournal : IDisposable
{
    private readonly string _walPath;
    private FileStream? _walStream;
    private readonly object _lock = new();
    private bool _disposed;

    public string WalPath => _walPath;

    public WalJournal(string databasePath)
    {
        _walPath = $"{databasePath}.wal";
        _walStream = new FileStream(
            _walPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);
    }

    /// <summary>
    /// Appends a journal operation and flushes to disk for durability.
    /// </summary>
    public void Append(AmeWalOpType opType, ReadOnlySpan<byte> payload)
    {
        lock (_lock)
        {
            if (_walStream == null) return;

            uint crc = PayloadManager.ComputeCrc32(payload);
            uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var header = new AmeWalRecordHeader
            {
                OpType = (byte)opType,
                Reserved = 0,
                HeaderChecksum = 0,
                DataLength = (uint)payload.Length,
                DataChecksum = crc,
                Timestamp = now
            };

            _walStream.Seek(0, SeekOrigin.End);
            var headerBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref header, 1));
            _walStream.Write(headerBytes);
            _walStream.Write(payload);
            _walStream.Flush(flushToDisk: true);
        }
    }

    /// <summary>
    /// Truncates the WAL file after a successful database checkpoint.
    /// </summary>
    public void Checkpoint()
    {
        lock (_lock)
        {
            if (_walStream == null) return;
            _walStream.SetLength(0);
            _walStream.Flush(flushToDisk: true);
        }
    }

    /// <summary>
    /// Checks if there are pending uncheckpointed records in the WAL journal.
    /// </summary>
    public bool HasPendingRecords => _walStream != null && _walStream.Length > 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _walStream?.Dispose();
        _walStream = null;
    }
}
