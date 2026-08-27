using AgentMemoryEngine.Core.WorkingMemory;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class WorkingMemoryTests
{
    [Fact]
    public void Fork_Rollback_LeavesParentPristine()
    {
        var parentFrame = new WorkingFrame("Refactor Inventory Service");
        parentFrame.TouchFile("src/InventoryService.cs", "// original code");
        parentFrame.AddActiveSymbol(42);

        // Create speculative fork
        var fork = parentFrame.Fork("Hypothesis: Remove sync locks");
        fork.TouchFile("src/LockHelper.cs", "// modified locks");
        fork.AddEvidence("Compiler error CS0103");
        fork.AddActiveSymbol(99);

        // Verify fork has modified state
        Assert.Equal(2, fork.TouchedFiles.Count);
        Assert.Single(fork.DiagnosticEvidence);
        Assert.Equal(2, fork.ActiveSymbolIds.Count);

        // Rollback speculative fork
        fork.Rollback();
        Assert.Equal(AmeFrameStatus.RolledBack, fork.Status);

        // Verify parent frame remains completely unmodified
        Assert.Single(parentFrame.TouchedFiles);
        Assert.Contains("src/InventoryService.cs", parentFrame.TouchedFiles);
        Assert.DoesNotContain("src/LockHelper.cs", parentFrame.TouchedFiles);
        Assert.Empty(parentFrame.DiagnosticEvidence);
        Assert.Single(parentFrame.ActiveSymbolIds);
        Assert.Contains(42u, parentFrame.ActiveSymbolIds);
    }

    [Fact]
    public void Fork_Merge_AppliesDeltaToParentFrame()
    {
        var parentFrame = new WorkingFrame("Fix NullReferenceException");
        parentFrame.TouchFile("src/MainController.cs");

        var fork = parentFrame.Fork("Hypothesis: Null check before dispatch");
        fork.TouchFile("src/Dispatcher.cs", "+ if (x == null) return;");
        fork.AddEvidence("Unit test passed: 100%");
        fork.AddActiveSymbol(88);

        // Merge speculative fork
        fork.Merge();
        Assert.Equal(AmeFrameStatus.Committed, fork.Status);

        // Parent frame must now contain merged delta
        Assert.Equal(2, parentFrame.TouchedFiles.Count);
        Assert.Contains("src/Dispatcher.cs", parentFrame.TouchedFiles);
        Assert.Single(parentFrame.DiagnosticEvidence);
        Assert.Equal("Unit test passed: 100%", parentFrame.DiagnosticEvidence[0]);
        Assert.Contains(88u, parentFrame.ActiveSymbolIds);
        Assert.Equal("Hypothesis: Null check before dispatch", parentFrame.ActiveHypothesis);
    }
}
