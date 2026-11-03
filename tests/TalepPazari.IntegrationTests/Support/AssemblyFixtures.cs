// Kept out of PostgresFixture.cs because TalepPazari.ApiTests links that file and uses the fixture
// per test class, so its tests that need no database never wait for (or fail on) Docker.
[assembly: AssemblyFixture(typeof(TalepPazari.IntegrationTests.Support.PostgresFixture))]
