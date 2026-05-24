# SQL Writes — Three Upsert Strategies

A .NET 10 solution demonstrating three production-ready upsert strategies for a `CustomerActivityEvent` record against SQL Server. All strategies are wrapped in a shared transient-fault retry policy.

## The Data Model

`CustomerActivityEvent` is a sealed record with:

| Field | Type | Description |
|---|---|---|
| `EventId` | `Guid` | Immutable event identity |
| `CustomerId` | `int` | Who performed the activity |
| `ActivityType` | `string` | What happened |
| `TimeStampUtc` | `DateTime` | When it happened |
| `DetailsJson` | `string` | JSON payload / metadata |
| `RowVersion` | `byte[]` | Current row version (from DB) |
| `ExpectedRowVersion` | `byte[]` | Optimistic concurrency check value |

Upsert logic applies updates only when the incoming `TimeStampUtc` is newer than the stored value, providing built-in out-of-order protection.

## Upsert Strategies

### 1. Normal — `UseNormal`

**Best for:** low volume, simple correctness requirements.

- Opens a transaction per event
- `UPDATE ... WHERE event_id = @id AND time_stamp_utc < @new_ts` (with optional `row_version` check)
- `INSERT ... WHERE NOT EXISTS (SELECT 1 ... WITH (UPDLOCK, HOLDLOCK))`
- Commits; rolls back on failure
- Supports `UpsertAsync` (single event) and `UpsertManyAsync` (loop over a list)

### 2. TVP — `UseTvp`

**Best for:** medium-volume batch ingestion via stored procedures.

- Builds a `DataTable` from a list of events
- Passes it as a Table-Valued Parameter to `dbo.UpsertCustomerActivityEventsType`
- The stored procedure deduplicates, updates stale rows, and inserts new ones in a single transaction

### 3. Bulk Staging — `UseBulk`

**Best for:** high-volume ingestion pipelines (tested with 200 000 events).

- Generates a `batchId` (GUID)
- Streams events into `dbo.CustomerActivityEvents_Staging` via `SqlBulkCopy` (batch size: 50 000, table lock, streaming enabled)
- Calls `dbo.ReconcileCustomerActivityEventsBatch` which deduplicates, updates, and inserts — returning `RowsUpdated`, `RowsInserted`, `RowsConflicted` as OUTPUT parameters
- Cleans up the staging table after reconciliation

## Strategy Comparison

| Strategy | Best For | Notes |
|---|---|---|
| Normal | Low volume / simplicity | Easiest to debug; 1 round trip per event |
| TVP | Medium-volume batches | Clean SQL boundary via stored proc |
| Bulk Staging | High-volume pipelines | Fastest; needs staging table and reconcile proc |

## Retry Policy (`Common/SqlRetryPolicy`)

Wraps all DB operations with exponential backoff and jitter. Retries on these transient SQL error codes: 1205 (deadlock), -2 (timeout), 4060, 40197, 40501, 40613, 49918, 49919, 49920, 10053, 10054, 10060, `TimeoutException`, and `IOException`.

Default: **5 retries**, base delay **100 ms**, max delay **5 000 ms**.

## Database Setup

Run `Query.sql` against your SQL Server database to create:

- `dbo.CustomerActivityEvents` — main table with `row_version` column and unique index on `event_id`
- `dbo.CustomerActivityEventType` — TVP type definition
- `dbo.UpsertCustomerActivityEventsType` — stored procedure for the TVP strategy
- `dbo.CustomerActivityEvents_Staging` — staging table for the bulk strategy
- `dbo.ReconcileCustomerActivityEventsBatch` — stored procedure for bulk reconciliation

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local or remote)

## Configuration

Edit the connection string in each project's `Program.cs`:

```csharp
var connectionString = "Data Source=<server>;Initial Catalog=<db>;Integrated Security=True;Trust Server Certificate=True";
```

## NuGet Packages

| Package | Version |
|---|---|
| `Microsoft.Data.SqlClient` | 6.1.3 |

## Build and Run

```bash
# Normal strategy demo
cd SQLWrites/UseNormal && dotnet run

# TVP strategy demo
cd SQLWrites/UseTvp && dotnet run

# Bulk Staging strategy demo (streams 200 000 events)
cd SQLWrites/UseBulk && dotnet run
```

## Project Structure

```
SQLWrites/
├── Common/
│   ├── Model.cs               # CustomerActivityEvent record
│   └── SqlRetryPolicy.cs      # Transient-fault retry logic
├── UseNormal/
│   ├── CustomerActivityEventWriter.cs            # Normal upsert
│   └── Program.cs
├── UseTvp/
│   ├── CustomerActivityEventTvpWriter.cs         # TVP batch upsert
│   └── Program.cs
├── UseBulk/
│   ├── CustomerActivityEventBulkStagingWriter.cs # Bulk copy + reconcile
│   ├── CustomerActivityEventDataReader.cs        # Custom IDataReader for SqlBulkCopy
│   └── Program.cs
├── Query.sql                  # SQL DDL: tables, types, stored procedures
└── SQLWrites.sln
```
