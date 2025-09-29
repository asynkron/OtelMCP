using Xunit;

namespace Asynkron.OtelReceiver.Tests;

[CollectionDefinition("GrpcIntegration", DisableParallelization = true)]
public class GrpcIntegrationCollection : ICollectionFixture<OtelReceiverApplicationFactory>
{
}
