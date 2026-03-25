# Stashframe API

## Project Structure

| Project | Purpose |
|---|---|
| `JSG.API.Stashframe` | ASP.NET Core web API (startup project) |
| `JSG.API.Stashframe.Core` | Domain entities, DbContext, EF configurations, and migrations |
| `JSG.API.Stashframe.Services` | Business logic / service layer |
| `JSG.API.Stashframe.Repositories` | Data access layer |

## Database

- **Provider:** PostgreSQL via [Npgsql](https://www.npgsql.org/efcore/)
- **ORM:** Entity Framework Core 10
- **DbContext:** `StashframeContext` in `JSG.API.Stashframe.Core/Database/`

### Configuration Pattern

Entity configurations use the `IEntityTypeConfiguration<T>` pattern and are auto-discovered via:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(StashframeContext).Assembly);
```

Configuration classes live in `JSG.API.Stashframe.Core/Database/Configuration/`.

### Connection String

Configured in `appsettings.json` under the `Postgres` connection string name and registered in `Program.cs`:

```csharp
builder.Services.AddDbContext<StashframeContext>(cfg =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    cfg.UseNpgsql(connectionString);
});
```

## Migrations

Migrations live in `JSG.API.Stashframe.Core/Data/Migrations/` and are managed using the `dotnet ef` CLI tool.

### Prerequisites

Install the EF Core CLI tool (one-time):

```bash
dotnet tool install --global dotnet-ef
```

### Commands

All commands should be run from the `api/` directory.

**Add a new migration:**

```bash
dotnet ef migrations add <MigrationName> \
  --project JSG.API.Stashframe.Core \
  --startup-project JSG.API.Stashframe \
  -o Data/Migrations
```

The `-o Data/Migrations` flag ensures migration files are output to `JSG.API.Stashframe.Core/Data/Migrations/`. This only needs to be specified when creating the first migration — subsequent migrations will automatically use the same directory.

**Apply migrations to the database:**

```bash
dotnet ef database update \
  --project JSG.API.Stashframe.Core \
  --startup-project JSG.API.Stashframe
```

**Remove the last unapplied migration:**

```bash
dotnet ef migrations remove \
  --project JSG.API.Stashframe.Core \
  --startup-project JSG.API.Stashframe
```

**List all migrations:**

```bash
dotnet ef migrations list \
  --project JSG.API.Stashframe.Core \
  --startup-project JSG.API.Stashframe
```

**Generate a SQL script (for production deployments):**

```bash
dotnet ef migrations script \
  --project JSG.API.Stashframe.Core \
  --startup-project JSG.API.Stashframe
```

### Adding a New Entity

1. Create the entity class in `JSG.API.Stashframe.Core/Database/Entities/`
2. Create an `IEntityTypeConfiguration<T>` in `JSG.API.Stashframe.Core/Database/Configuration/`
3. Add a `DbSet<T>` property to `StashframeContext`
4. Generate a migration using the command above
5. Apply the migration to update the database
