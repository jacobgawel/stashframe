# Sagas (MassTransit State Machines)

## What is a Saga?

A saga is a long-running process that coordinates multiple steps across different services or consumers. Instead of doing everything in a single request, a saga reacts to events over time — it receives a message, does some work (or tells someone else to do work), saves its progress, and waits for the next message.

In MassTransit, sagas are implemented as **state machines**. A state machine defines:

- **States** — where the saga currently is in its lifecycle (e.g., `Processing`, `Failed`)
- **Events** — messages that can trigger transitions between states (e.g., `UploadCompleted`, `ImageProcessed`)
- **Behaviors** — what happens when a specific event arrives during a specific state (e.g., publish a command, schedule a timeout, transition to a new state)

## Why Use a Saga?

Without a saga, a controller might try to do everything synchronously: validate, download, process, upload, update the database — all in one HTTP request. This has problems:

- **Timeouts** — if processing takes 30 seconds, the HTTP request times out
- **Retries** — if step 3 of 5 fails, you have to redo steps 1-2 on retry
- **Coordination** — if you need multiple consumers to do work (e.g., image processing + video transcoding), you need something to track when all of them are done

A saga solves this by acting as the **coordinator**. It doesn't do the work itself — it tells consumers what to do, tracks what's completed, and decides what happens next.

## How MassTransit Sagas Work

### Correlation

Every saga instance has a `CorrelationId` — a unique identifier (GUID) that ties related messages together. When a message arrives on the bus, MassTransit looks at the correlation configuration to extract an ID from the message, then uses that ID to find the matching saga instance in the persistence store (Redis in our case).

```csharp
// "When an UploadCompleted message arrives, use its MediaId to find the saga"
Event(() => UploadCompleted, x =>
    x.CorrelateById(ctx => ctx.Message.MediaId));

// "When an ImageProcessed message arrives, use its CorrelationId to find the saga"
Event(() => ImageProcessed, x =>
    x.CorrelateById(ctx => ctx.Message.CorrelationId));
```

This is how MassTransit knows which saga instance a message belongs to. If you have 100 images being processed concurrently, there are 100 saga instances in Redis, each with a different `CorrelationId`. An incoming `ImageProcessed` message is routed to exactly the right one.

### State

The saga's current state determines which events it will accept. This is defined using `Initially(...)` and `During(...)` blocks:

```csharp
// Only accepts events when no saga instance exists yet
Initially(
    When(UploadCompleted)
        // ... create the saga, do initial work
        .TransitionTo(Processing)
);

// Only accepts events when the saga is in the Processing state
During(Processing,
    When(ImageProcessed)
        // ... handle completion
        .Finalize()
);
```

If an event arrives that isn't handled in the current state, MassTransit throws an `UnhandledEventException` by default. To silently discard events that can arrive but shouldn't trigger anything, use `Ignore()`:

```csharp
During(Processing,
    Ignore(UploadCompleted)  // discard duplicates silently
);
```

### Saga Instance Lifecycle

1. **Creation** — When an event in the `Initially` block arrives and no matching saga exists, MassTransit creates a new instance. The `CorrelationId` is set from the message's correlated value.

2. **Persistence** — After each event is handled, MassTransit saves the saga state to the persistence store (Redis). This means the saga survives process restarts — if the app crashes and comes back, the saga picks up where it left off.

3. **Transitions** — `TransitionTo(State)` changes the saga's `CurrentState`. This determines which `During(...)` block handles the next event. MassTransit increments the `Version` field on each transition for concurrency control.

4. **Finalization** — `.Finalize()` marks the saga as complete. When combined with `SetCompletedWhenFinalized()`, MassTransit deletes the saga instance from the persistence store. Without this, finalized sagas would accumulate.

### Events vs Commands

Sagas work with two types of messages:

- **Events** (past tense) — something that happened: `UploadCompleted`, `ImageProcessed`. These are published to the bus and any subscriber can receive them. The saga listens for these to know when to transition.
- **Commands** (imperative) — instructions to do something: `ProcessImage`. These are sent to a specific consumer's queue. The saga publishes these to kick off work.

The pattern is: saga receives an **event** -> saga publishes a **command** -> consumer does work -> consumer publishes an **event** back -> saga receives it and continues.

### Scheduling and Timeouts

MassTransit can schedule messages for future delivery. This is how timeouts work:

```csharp
Schedule(() => ProcessingTimeout, x => x.TimeoutTokenId,
    s =>
    {
        s.Delay = TimeSpan.FromMinutes(5);
        s.Received = e => e.CorrelateById(ctx => ctx.Message.CorrelationId);
    });
```

This tells MassTransit: "After 5 minutes, deliver a `ProcessingTimedOut` message to this saga." The `TimeoutTokenId` is a GUID stored on the saga state that identifies this specific scheduled message.

- **Schedule** — queues a message for future delivery via Azure Service Bus delayed delivery
- **Unschedule** — cancels a previously scheduled message using the stored token ID
- The timeout message correlates back to the saga just like any other event

This is the safety net — if a consumer dies and never responds, the timeout fires and the saga can clean up, notify, or retry.

### Persistence (Redis)

Saga state is stored in Redis as a serialized object. Each saga instance is a separate key, identified by `CorrelationId`. Redis was chosen because:

- **Fast** — saga state is read and written on every event, so low latency matters
- **TTL support** — Redis can automatically expire keys if needed
- **Atomic operations** — Redis supports optimistic concurrency via the `Version` field

MassTransit handles all serialization and deserialization automatically. The saga state class just needs to implement `SagaStateMachineInstance` and `ISagaVersion`:

```csharp
public class MediaProcessingState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }  // required by MassTransit
    public string CurrentState { get; set; }  // managed by MassTransit
    public int Version { get; set; }  // concurrency control

    // Your custom fields
    public Guid MediaId { get; set; }
    public MediaCategory Category { get; set; }
    // ...
}
```

### Concurrency

When two messages for the same saga arrive simultaneously, MassTransit uses the `Version` field for optimistic concurrency:

1. Both handlers read the saga from Redis (e.g., `Version = 3`)
2. Handler A finishes first, writes back with `Version = 4` — succeeds
3. Handler B tries to write with `Version = 4` — fails because Redis already has `Version = 4`
4. MassTransit retries Handler B, which re-reads the saga (now at `Version = 4`) and tries again

This prevents two events from corrupting the saga state by writing over each other.

## Key Concepts Summary

| Concept | Description |
|---|---|
| **CorrelationId** | Unique ID linking all messages in a saga instance together |
| **State** | Where the saga is in its lifecycle — determines which events are accepted |
| **Initially** | Handles events that create new saga instances |
| **During** | Handles events for sagas in a specific state |
| **Ignore** | Silently discards an event during a state instead of throwing |
| **TransitionTo** | Changes the saga's current state |
| **Finalize** | Marks the saga as complete (deleted from persistence with `SetCompletedWhenFinalized`) |
| **Schedule** | Queues a message for future delivery (timeouts) |
| **Unschedule** | Cancels a previously scheduled message |
| **Publish** | Sends an event/command to the bus from within a saga handler |
| **Version** | Incremented on each state change — used for optimistic concurrency in Redis |
