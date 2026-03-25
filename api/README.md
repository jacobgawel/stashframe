# Stashframe API

A media management API built with ASP.NET Core (.NET 10) that handles uploads, image processing, and sharing via Azure Blob Storage and a MassTransit saga pipeline.

## Tech Stack

| Technology | Purpose |
|---|---|
| ASP.NET Core 10 | Web API framework |
| PostgreSQL 17 | Primary database |
| Entity Framework Core 10 | ORM |
| Azure Blob Storage (Azurite locally) | Media file storage |
| Azure Service Bus | Message broker |
| MassTransit | Saga orchestration and consumers |
| Redis | Saga state persistence and distributed cache |
| SixLabors.ImageSharp | Image processing |
| Serilog | Structured logging |

## Project Structure

| Project | Purpose |
|---|---|
| `JSG.API.Stashframe` | ASP.NET Core web API (startup project) |
| `JSG.API.Stashframe.Core` | Domain entities, DbContext, EF configurations, interfaces, enums, constants, saga definitions, and migrations |
| `JSG.API.Stashframe.Services` | Business logic, image processing, and MassTransit consumers |
| `JSG.API.Stashframe.Repositories` | Data access layer |

```
api/
├── JSG.API.Stashframe/               # Startup project
│   ├── Controllers/                   # API endpoints
│   ├── Extensions/                    # Blob storage setup
│   ├── Program.cs                     # Service registration & middleware
│   └── appsettings*.json             # Configuration
├── JSG.API.Stashframe.Core/          # Core domain
│   ├── Constants/                     # BlobContainers, BlobPaths, MediaLimits, SupportedMedia
│   ├── Data/Migrations/              # EF Core migrations
│   ├── Database/
│   │   ├── Configuration/            # IEntityTypeConfiguration classes
│   │   ├── Entities/                 # Media, ShareLink
│   │   └── StashframeContext.cs      # DbContext
│   ├── Enums/                        # MediaCategory, MediaStatus, OutputFormat, ShareVisibility
│   ├── Interfaces/                   # Service & repository contracts
│   ├── Models/                       # UploadRequest, SasUploadResult
│   └── Sagas/                        # MassTransit saga definitions & contracts
├── JSG.API.Stashframe.Services/      # Service implementations
│   ├── Consumers/                    # ProcessImageConsumer
│   ├── ImageProcessingService.cs
│   └── MediaStorageService.cs
└── JSG.API.Stashframe.Repositories/  # Repository implementations
    └── MediaStorageRepository.cs
```

## API Endpoints

### Upload Controller (`/api/upload`)

| Method | Route | Description |
|---|---|---|
| POST | `/api/upload` | Validates the MIME type, creates a `Media` record, and returns a 15-minute SAS upload URL for the client to upload directly to Azure Blob Storage |
| POST | `/api/upload/confirm` | Publishes an `UploadCompleted` event to the message bus, triggering the media processing saga |

## Media Processing Pipeline

The processing pipeline uses a MassTransit state machine saga backed by Redis.

```
Client Upload
    │
    ▼
POST /api/upload          → Create Media record + return SAS URL
    │
    ▼
Client uploads to blob    → Direct-to-Azure via SAS token
    │
    ▼
POST /api/upload/confirm  → Publish UploadCompleted event
    │
    ▼
MediaProcessingSaga       → Starts saga, schedules 5-min timeout
    │
    ├─ Screenshot category → Publish ProcessImage command
    │       │
    │       ▼
    │   ProcessImageConsumer
    │       ├─ Download raw blob
    │       ├─ Extract metadata (width, height)
    │       ├─ Optimise → full-size WebP
    │       └─ Generate thumbnails (sm:320px, md:640px, lg:1280px)
    │       │
    │       ▼
    │   Publish ImageProcessed
    │       │
    │       ▼
    │   Saga finalizes → Publish MediaReady (for SignalR)
    │
    └─ Timeout (5 min) → Saga transitions to Failed
```

### Saga States

`Initial` → `Processing` → `Finalized` (success) or `Failed` (timeout)

### Message Contracts

| Contract | Type | Description |
|---|---|---|
| `UploadCompleted` | Event | Triggers the saga (MediaId, Category) |
| `ProcessImage` | Command | Instructs consumer to process an image |
| `ImageProcessed` | Event | Consumer signals processing complete |
| `MediaReady` | Event | Saga signals media is ready (for SignalR) |
| `ProcessingTimedOut` | Event | 5-minute timeout marker |

## Blob Storage

### Containers

| Container | Purpose |
|---|---|
| `stashframe-raw` | Original uploaded files |
| `stashframe-transcoded` | Video transcoding outputs (HLS) |
| `stashframe-thumbnails` | Resized thumbnail variants |
| `stashframe-screenshots` | Full-size processed images |

Containers are auto-created at startup via `EnsureBlobContainersAsync()`.

### Blob Path Conventions

| Path | Example |
|---|---|
| Raw original | `{userId}/{mediaId}/original.{ext}` |
| Raw metadata | `{userId}/{mediaId}/metadata.json` |
| Screenshot full | `{mediaId}/full.webp` |
| Thumbnail variant | `{mediaId}/thumb_{sm\|md\|lg}.webp` |
| Avatar | `{userId}/avatar_{variant}.webp` |
| Banner | `{userId}/banner.webp` |
| Export | `{userId}/{exportId}.zip` |

## Supported Media Types

| Category | MIME Types |
|---|---|
| Screenshot | `image/png`, `image/jpeg`, `image/webp`, `image/bmp`, `image/tiff` |
| Video | `video/mp4`, `video/webm`, `video/x-matroska`, `video/quicktime`, `video/x-msvideo` |
| Animated Image | `image/gif`, `image/apng` |

## Upload Limits

| Limit | Free | Pro |
|---|---|---|
| Max video size | 500 MB | 2 GB |
| Max screenshot size | 50 MB | 100 MB |
| Daily uploads | 20 | 100 |

## Database

- **Provider:** PostgreSQL via [Npgsql](https://www.npgsql.org/efcore/)
- **ORM:** Entity Framework Core 10
- **DbContext:** `StashframeContext` in `JSG.API.Stashframe.Core/Database/`

### Entities

**Media** (`media` table)
- Tracks uploaded files with metadata (filename, MIME type, size, dimensions, duration)
- Status lifecycle: `Pending` → `Processing` → `PartialReady` → `Ready` (or `Failed` / `Deleted`)
- Stores the raw blob path for retrieval

**ShareLink** (`share_links` table)
- Enables sharing media via human-readable slugs
- Supports visibility levels: `Public`, `Unlisted`, `Private`
- Optional password protection and expiry
- Tracks view count

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

## Local Development

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose

### Docker Services

The `docker-compose.yml` in the repo root provides local infrastructure:

| Service | Port | Purpose |
|---|---|---|
| PostgreSQL 17 | 5432 | Database (`stashframe` / `stashframe`) |
| Redis | 6379 | Saga state & distributed cache |
| Azurite | 10000-10002 | Azure Blob Storage emulator |

```bash
docker compose up -d
```

### Running the API

```bash
cd api
dotnet run --project JSG.API.Stashframe
```

The API launches on `https://localhost:7042` / `http://localhost:5236` by default.

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
