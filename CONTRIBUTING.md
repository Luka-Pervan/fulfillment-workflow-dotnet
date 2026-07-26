# Contributing

1. Create a focused branch.
2. Run `dotnet format Fulfillment.slnx`.
3. Run `dotnet build Fulfillment.slnx`.
4. Run `dotnet test Fulfillment.slnx --filter "Category!=Postgres"`.
5. Run `dotnet list Fulfillment.slnx package --vulnerable --include-transitive`.
6. Explain behavior, tests, security impact, and tradeoffs in the pull request.

Changes to EF mappings, concurrency, transactions, or provider-specific queries
must also run the `Category=Postgres` test against a migrated disposable
PostgreSQL database.

Never commit real credentials, customer data, or code copied from a private
project.
