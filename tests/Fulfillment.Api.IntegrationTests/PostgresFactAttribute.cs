using Xunit;

namespace Fulfillment.Api.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    PostgresPersistenceTests.ConnectionStringVariable)))
        {
            Skip = $"Set {PostgresPersistenceTests.ConnectionStringVariable} "
                + "to run the real PostgreSQL functional tests.";
        }
    }
}
