namespace Kable.SharedMemory.Tests.Memory;

using System.IO.MemoryMappedFiles;
using Kable.SharedMemory.Memory;
using Xunit;

public class RingBufferResourceTests
{
    [Fact]
    public void PartialAcquisition_DisposeClosesEveryOwnedHandleWithoutGc()
    {
        string name = "resources_" + Guid.NewGuid().ToString("N");
        var resources = new RingBufferResources
        {
            Mapping = MemoryMappedFile.CreateNew(name + "_mmf", 512),
            Data = RingBufferResources.CreateEvent(name + "_data_evt"),
            Space = RingBufferResources.CreateEvent(name + "_space_evt")
        };
        resources.Map();
        var mapping = resources.Mapping.SafeMemoryMappedFileHandle;
        var view = resources.View!.SafeMemoryMappedViewHandle;
        var data = resources.Data.SafeWaitHandle;
        var space = resources.Space.SafeWaitHandle;
        resources.Dispose();
        Assert.True(mapping.IsClosed);
        Assert.True(view.IsClosed);
        Assert.True(data.IsClosed);
        Assert.True(space.IsClosed);
    }
}
