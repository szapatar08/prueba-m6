using Xunit;

namespace Prueba.IntegrationTests;

/// <summary>
/// Collection definition to force all integration test classes to run sequentially.
/// This prevents database conflicts when tests share the same SQLite in-memory database.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<PruebaWebApplicationFactory>
{
    // This class has no code and is never created.
    // Its purpose is to apply the [CollectionDefinition] attribute.
}
