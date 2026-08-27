using System.Runtime.InteropServices;
using AgentMemoryEngine.Core.BinaryLayout;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class BinaryLayoutTests
{
    [Fact]
    public void GlobalHeader_MustBeExactly64Bytes()
    {
        Assert.Equal(64, Marshal.SizeOf<AmeGlobalHeader>());
    }

    [Fact]
    public void SegmentDescriptor_MustBeExactly32Bytes()
    {
        Assert.Equal(32, Marshal.SizeOf<AmeSegmentDescriptor>());
    }

    [Fact]
    public void CognitiveRecord_MustBeExactly32Bytes()
    {
        Assert.Equal(32, Marshal.SizeOf<AmeCognitiveRecord>());
    }

    [Fact]
    public void VectorHeader_MustBeExactly16Bytes()
    {
        Assert.Equal(16, Marshal.SizeOf<AmeVectorHeader>());
    }

    [Fact]
    public void PayloadHeader_MustBeExactly16Bytes()
    {
        Assert.Equal(16, Marshal.SizeOf<AmePayloadHeader>());
    }
}
