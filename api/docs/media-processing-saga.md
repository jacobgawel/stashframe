# Media Processing Saga

## Overview

The `MediaProcessingSaga` is a MassTransit state machine that orchestrates media processing after a client uploads a file. It coordinates the flow from upload confirmation through image processing to completion, with a timeout safety net for failed processing.

Saga state is persisted in **Redis** and events are transported via **Azure Service Bus**.

## State Diagram

```
                  UploadCompleted
                       |
                       v
               +--[Initially]--+
               |               |
               | Populate state|
               | Publish ProcessImage (if Screenshot)
               | Schedule 5min timeout
               |               |
               v               |
          [Processing] <-------+
           /      |    \
          /       |     \
         v        v      v
  ImageProcessed  Timeout  UploadCompleted
         |          |           |
         |          |        (Ignored)
         v          v
   Unschedule   Publish
    timeout     FailedProcessing
      |            |
   Publish         v
   MediaReady   Finalize
      |         (cleanup)
      v
   Finalize
   (cleanup)
```

## States

| State | Description |
|---|---|
| **Initial** | Saga does not exist yet. Waiting for `UploadCompleted`. |
| **Processing** | Media is being processed by a consumer. Timeout is scheduled. |
| **Final** | Saga is complete and removed from Redis. |

## Events

| Event | Published By | Correlates By | Description |
|---|---|---|---|
| `UploadCompleted` | `UploadController` | `MediaId` | Client has confirmed the upload. Triggers saga creation. |
| `ProcessImage` | Saga | — | Command sent to `ProcessImageConsumer` to begin processing. |
| `ImageProcessed` | `ProcessImageConsumer` | `CorrelationId` | Processing completed successfully. |
| `ProcessingTimedOut` | MassTransit Scheduler | `CorrelationId` | 5-minute timeout fired. Processing assumed failed. |
| `MediaReady` | Saga | — | Media is fully processed and available. Will be consumed via SignalR later. |
| `FailedProcessing` | Saga | — | Processing timed out. Consumer should update DB status to `Failed`. |

## Contracts

### UploadCompleted
```csharp
public record UploadCompleted
{
    public Guid MediaId { get; set; }
    public MediaCategory Category { get; set; }
}
```

### ProcessImage
```csharp
public record ProcessImage
{
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}
```

### ImageProcessed
```csharp
public record ImageProcessed
{
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}
```

### MediaReady
```csharp
public record MediaReady
{
    public Guid MediaId { get; set; }
    public Guid UserId { get; set; }
}
```

### FailedProcessing
```csharp
public record FailedProcessing
{
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}
```

### ProcessingTimedOut
```csharp
public record ProcessingTimedOut : CorrelatedBy<Guid>
{
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}
```

## Saga State (Redis)

The `MediaProcessingState` instance is stored in Redis, keyed by `CorrelationId`:

| Field | Type | Description |
|---|---|---|
| `CorrelationId` | `Guid` | Saga instance identifier (matches `MediaId` on creation) |
| `CurrentState` | `string` | Current state name (`Processing`, etc.) |
| `MediaId` | `Guid` | The media record ID |
| `UserId` | `Guid` | The uploading user's ID |
| `BlobPath` | `string` | Path to the raw blob |
| `Category` | `MediaCategory` | Media category (e.g., `Screenshot`) |
| `ImageProcessed` | `bool` | Completion flag (for future multi-step flows) |
| `TimeoutTokenId` | `Guid?` | Token for the scheduled timeout message |
| `Created` | `DateTime` | When the saga was created |
| `Updated` | `DateTime` | Last state transition time |
| `Version` | `int` | Concurrency version (incremented by MassTransit on each transition) |

## Flow Detail

### Happy Path

1. Client uploads a file directly to Azure Blob Storage using a SAS URL obtained from `POST /api/upload`
2. Client calls `POST /api/upload/{mediaId}/confirm` to signal the upload is complete
3. The controller runs two validation checks:
   - **Blob existence**: Looks up the `Media` record by ID, then calls `BlobClient.ExistsAsync()` against the `raw` container using the stored `RawBlobPath`. If the blob isn't there, returns `400 Bad Request` — the client confirmed without actually uploading
   - **Atomic claim**: Executes `UPDATE Media SET MediaStatus = 'Processing' WHERE Id = @id AND MediaStatus = 'Pending'`. This is a single SQL statement — the database locks the row, so only one request can transition from `Pending` to `Processing`. If `rows == 0`, someone else already claimed it and the controller returns `409 Conflict`
4. The controller publishes an `UploadCompleted` message onto Azure Service Bus with the `MediaId` and the `Category` (read from the database, not from the client request)
5. MassTransit receives `UploadCompleted` on the bus. It uses the event correlation config (`CorrelateById(ctx => ctx.Message.MediaId)`) to look up a saga instance in Redis with `CorrelationId == MediaId`. No instance exists yet
6. Because the event is handled in the `Initially` block, MassTransit **creates a new `MediaProcessingState` instance** in Redis. The `CorrelationId` is set to the `MediaId` from the message. The `Version` field starts at `0`
7. The `Then` block executes — populates the saga state fields: `MediaId`, `Category`, `Created`, and `Updated` timestamps. These values are persisted to Redis and available to all subsequent event handlers
8. The `If` block evaluates `ctx.Message.Category == MediaCategory.Screenshot`. If true, the saga publishes a `ProcessImage` command onto the bus. This message carries both the `MediaId` and the saga's `CorrelationId` — the consumer will use the `CorrelationId` to publish its response back so MassTransit can route it to the correct saga instance
9. `Schedule(ProcessingTimeout, ...)` tells MassTransit to publish a `ProcessingTimedOut` message after 5 minutes via the Azure Service Bus delayed delivery. The `TimeoutTokenId` is saved on the saga state — this GUID is used later if the timeout needs to be cancelled
10. `TransitionTo(Processing)` sets `CurrentState = "Processing"` on the saga instance. MassTransit increments `Version` and saves the full state to Redis. The saga is now waiting for either `ImageProcessed` or the timeout
11. The `ProcessImageConsumer` picks up the `ProcessImage` message from its Azure Service Bus queue. It loads the `Media` record from PostgreSQL, downloads the raw blob from Azure Blob Storage, decodes the image, generates a full-size WebP optimisation, and creates thumbnails at 320px, 640px, and 1280px. Each processed file is uploaded to the appropriate blob container (`screenshots` or `thumbnails`)
12. Once processing is complete, the consumer updates the `Media` record in PostgreSQL (status to `Ready`, dimensions, processed size) and publishes an `ImageProcessed` message with the `CorrelationId` it received from the saga
13. MassTransit receives `ImageProcessed` on the bus. Using the correlation config (`CorrelateById(ctx => ctx.Message.CorrelationId)`), it finds the saga instance in Redis. The saga is in the `Processing` state, so MassTransit evaluates the `During(Processing, ...)` handlers
14. The `When(ImageProcessed)` handler matches. It first calls `Unschedule(ProcessingTimeout)` — this uses the `TimeoutTokenId` stored on the saga to cancel the pending `ProcessingTimedOut` message so it never fires
15. The saga publishes a `MediaReady` message with the `MediaId` and `UserId`. This event signals that the media is fully processed and available — it will be consumed by a SignalR service later to notify the client in real-time
16. `.Finalize()` marks the saga as complete. Because `SetCompletedWhenFinalized()` is configured, MassTransit **deletes the saga instance from Redis**. The saga is fully cleaned up — no orphaned state

### Timeout Path

1. Steps 1-10 from the happy path complete — the saga is in `Processing` and the `ProcessImageConsumer` has received the `ProcessImage` command
2. The consumer fails — the image is corrupt, the blob download throws, or the process crashes. The `ImageProcessed` message is never published
3. After 5 minutes, Azure Service Bus delivers the `ProcessingTimedOut` message that was scheduled in step 9. MassTransit correlates it to the saga instance via `CorrelationId`
4. The `When(ProcessingTimeout.Received)` handler matches. The saga publishes a `FailedProcessing` message with the `MediaId` and `CorrelationId`
5. `.Finalize()` marks the saga as complete and MassTransit deletes the instance from Redis
6. A consumer for `FailedProcessing` should update the `Media` record in PostgreSQL from `Processing` to `Failed`, so the client can be informed and the record doesn't stay stuck

### Duplicate Confirm

1. Client calls `POST /api/upload/{mediaId}/confirm` a second time for the same `MediaId`
2. The controller's atomic guard runs: `UPDATE Media SET MediaStatus = 'Processing' WHERE Id = @id AND MediaStatus = 'Pending'`. The row is already `Processing` from the first request, so the `WHERE` clause matches zero rows. The method returns `false` and the controller responds with `409 Conflict`. No message is published to the bus
3. Even if a duplicate `UploadCompleted` message somehow reaches the bus (e.g., from a retry policy), the saga is already in the `Processing` state. The `Ignore(UploadCompleted)` directive in `During(Processing, ...)` tells MassTransit to silently consume and discard the message without throwing an exception or transitioning state

## Infrastructure

| Component | Purpose |
|---|---|
| **Azure Service Bus** | Message transport (events and commands) |
| **Redis** | Saga state persistence and timeout scheduling |
| **Azure Blob Storage** | Raw uploads and processed media output |
| **PostgreSQL** | `Media` entity persistence (status, metadata) |
