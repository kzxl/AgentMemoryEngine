using System.Text;
using AgentMemoryEngine.Core.Storage;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class WalJournalTests : IDisposable
{
    private readonly string _tempDbPath;

    public WalJournalTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ame_wal_test_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        string walPath = $"{_tempDbPath}.wal";
        if (File.Exists(walPath))
        {
            try { File.Delete(walPath); } catch { /* ignore */ }
        }
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Wal_AppendAndCheckpoint_PersistsAndClears()
    {
        using (var wal = new WalJournal(_tempDbPath))
        {
            Assert.False(wal.HasPendingRecords);

            byte[] opData = Encoding.UTF8.GetBytes("INSERT_OP: ID=1, Tier=Episodic");
            wal.Append(AmeWalOpType.InsertRecord, opData);

            Assert.True(wal.HasPendingRecords);
            Assert.True(File.Exists(wal.WalPath));

            // Checkpoint clears pending records
            wal.Checkpoint();
            Assert.False(wal.HasPendingRecords);
        }
    }
}
