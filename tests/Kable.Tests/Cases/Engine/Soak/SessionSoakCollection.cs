namespace Kable.Tests.Cases.Engine;

using Xunit;

// Serialize load/memory-delta tests and external benchmark adapters (e.g. DotNetty 16MB pooled arena).
[CollectionDefinition("SessionSoakTests", DisableParallelization = true)]
public sealed class SessionSoakCollection
{
}
