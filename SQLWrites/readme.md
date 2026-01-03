# ⚡ Customer Activity Event Upserts — Normal vs TVP vs Bulk Staging (with SQL Retries)

When your system is **ingesting a flood of customer activity events**, the database write strategy you choose can mean the difference between:

✅ smooth, scalable throughput  
vs  
🔥 timeouts, deadlocks, and “why is prod on fire again?”

This project gives you **three production-ready upsert paths** for `CustomerActivityEvent` — each designed for a different volume + latency profile — all wrapped in a **SQL transient-fault retry policy**.

> **Goal:** reliably upsert customer activity events into SQL Server while balancing **throughput**, **locking**, **row-version concurrency**, and **operational safety**. :contentReference[oaicite:0]{index=0}

---

## 🧠 The Data Contract

At the core is a compact event record:

- `EventId` (Guid) — immutable identity
- `CustomerId` (int) — who did it
- `ActivityType` (string) — what happened
- `TimeStampUtc` (DateTime) — when it happened
- `DetailsJson` (string) — payload / metadata
- `RowVersion` + `ExpectedRowVersion` (byte[]) — optimistic concurrency hooks

This design supports **idempotency**, **ordering rules**, and **concurrency-safe updates**. :contentReference[oaicite:1]{index=1}

---

## 🛡️ Resilience First: SQL Retry Policy

Transient SQL errors happen. Networks blip. Azure throttles. Deadlocks occur.

`SqlRetryPolicy` wraps your operations with:

- **exponential backoff**
- **jitter**
- **known transient SQL error detection**
- optional logging hook

In short: *don’t fail fast — fail smart.* :contentReference[oaicite:2]{index=2}

---

# 🚀 Three Upsert Strategies (Pick Your Weapon)

## 1) 🧱 “Normal” Upsert — Simple, Safe, Slower

**File:** `CustomerActivityEventWriter`  
**Best for:** low volume, tight correctness, easiest debugging

How it works:
- opens a transaction
- `UPDATE ... WHERE event_id = @event_id AND time_stamp_utc < @time_stamp_utc`
- optional `row_version = @expected_row_version` check
- then `INSERT ... WHERE NOT EXISTS (...)` with `UPDLOCK, HOLDLOCK`

✅ Easy to reason about  
✅ Works great for small batches  
⚠️ One round trip per event (in `UpsertManyAsync`) → can get expensive fast :contentReference[oaicite:3]{index=3}

---

## 2) 📦 TVP Batch Upsert — Fast Batching, Less Chattiness

**File:** `CustomerActivityEventTvpWriter`  
**Best for:** medium volume, batch ingestion, stored-proc-centric systems

How it works:
- builds a `DataTable`
- sends it as a **Table-Valued Parameter** (`dbo.CustomerActivityEventType`)
- calls stored procedure `dbo.UpsertCustomerActivityEventsType`

✅ Fewer network round trips  
✅ Clean boundary: app sends data, SQL owns the merge logic  
⚠️ You still pay for DataTable creation + memory for large batches :contentReference[oaicite:4]{index=4}

---

## 3) 🏗️ Bulk Copy to Staging + Reconcile — Maximum Throughput

**File:** `CustomerActivityEventBulkStagingWriter`  
**Best for:** large volume, ingestion pipelines, “we process *a lot*”

How it works:
1. generate a `batchId`
2. `SqlBulkCopy` into `dbo.CustomerActivityEvents_Staging`
   - table lock
   - internal transaction
   - streaming enabled
   - huge batch size (`50,000`)
3. call stored procedure `dbo.ReconcileCustomerActivityEventsBatch`
   - outputs: `RowsUpdated`, `RowsInserted`, `RowsConflicted`

✅ Designed for *serious* throughput  
✅ Metrics built-in: you get updated/inserted/conflicted counts  
✅ Ideal for event ingestion services  
⚠️ Requires staging table + reconcile proc (schema discipline matters) :contentReference[oaicite:5]{index=5}

---

# 🎯 Which One Should You Use?

| Strategy | Best For | Pros | Watch Outs |
|---|---|---|---|
| Normal | low volume / simplicity | easiest to debug | lots of round trips |
| TVP | medium volume batching | clean SQL boundary | DataTable overhead |
| Bulk + Staging | high volume ingestion | fastest + metrics | needs staging + reconcile |

---

## ✨ Extra Highlights

### ✅ Ordering Rule Built In
Updates only apply when incoming `time_stamp_utc` is newer than what’s stored.  
That’s a subtle but powerful guardrail against out-of-order event arrivals. :contentReference[oaicite:6]{index=6}

### ✅ Optimistic Concurrency Ready
`ExpectedRowVersion` lets you enforce “update only if I’m not stale” behavior (when supplied). :contentReference[oaicite:7]{index=7}

### ✅ Operational Confidence
Every path is wrapped with `SqlRetryPolicy` so transient faults don’t become incidents. :contentReference[oaicite:8]{index=8}

---

# 🧪 Suggested Usage Patterns

- **API endpoint** receiving single events → *Normal*
- **worker** consuming batches from a queue → *TVP*
- **pipeline** draining high-throughput stream (Kafka/EventHub/etc.) → *Bulk Staging*

---

## 🧩 What You’ll Need in SQL (Conceptually)

To fully run the TVP and Bulk paths, your DB needs:

- `dbo.CustomerActivityEventType` (TVP type)
- `dbo.UpsertCustomerActivityEventsType` (stored proc)
- `dbo.CustomerActivityEvents_Staging` (staging table)
- `dbo.ReconcileCustomerActivityEventsBatch` (stored proc)

(Those objects aren’t shown in the snippet, but the code is wired for them.) :contentReference[oaicite:9]{index=9}

---

## ✅ Bottom Line

This is a **battle-tested write toolkit** for customer activity event upserts:

- correctness & concurrency ✅  
- retries & resilience ✅  
- scalable paths from small → massive ✅  

Pick the strategy that matches your throughput reality — and upgrade without rewriting your domain model.

---
