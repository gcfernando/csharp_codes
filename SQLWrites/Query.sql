SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.CustomerActivityEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerActivityEvents
    (
        event_id            UNIQUEIDENTIFIER    NOT NULL CONSTRAINT PK_CustomerActivityEvents PRIMARY KEY,
        customer_id         INT                 NOT NULL,
        activity_type       NVARCHAR(50)        NOT NULL,
        time_stamp_utc      DATETIME2(3)        NOT NULL,
        details_json        NVARCHAR(MAX)       NULL
    );
END
GO

IF COL_LENGTH('dbo.CustomerActivityEvents', 'row_version') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerActivityEvents
    ADD row_version ROWVERSION NOT NULL;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CustomerActivityEvents')
      AND name = 'IX_CustomerActivityEvents_EventId'
)
BEGIN
    DROP INDEX IX_CustomerActivityEvents_EventId ON dbo.CustomerActivityEvents;
END
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_CustomerActivityEvents_EventId
ON dbo.CustomerActivityEvents(event_id)
INCLUDE (time_stamp_utc, row_version);
GO

IF OBJECT_ID('dbo.UpsertCustomerActivityEventsType', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.UpsertCustomerActivityEventsType;
END
GO

IF TYPE_ID('dbo.CustomerActivityEventType') IS NOT NULL
BEGIN
    DROP TYPE dbo.CustomerActivityEventType;
END
GO

CREATE TYPE dbo.CustomerActivityEventType AS TABLE
(
    event_id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    customer_id           INT              NOT NULL,
    activity_type         NVARCHAR(50)     NOT NULL,
    time_stamp_utc        DATETIME2(3)     NOT NULL,
    details_json          NVARCHAR(MAX)    NULL,
    expected_row_version  VARBINARY(8)     NULL
);
GO

CREATE OR ALTER PROCEDURE dbo.UpsertCustomerActivityEventsType
    @Events dbo.CustomerActivityEventType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        CREATE TABLE #Src
        (
            event_id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            customer_id           INT              NOT NULL,
            activity_type         NVARCHAR(50)     NOT NULL,
            time_stamp_utc        DATETIME2(3)     NOT NULL,
            details_json          NVARCHAR(MAX)    NULL,
            expected_row_version  VARBINARY(8)     NULL
        );

        ;WITH Deduped AS
        (
            SELECT
                e.event_id,
                e.customer_id,
                e.activity_type,
                e.time_stamp_utc,
                e.details_json,
                e.expected_row_version,
                ROW_NUMBER() OVER
                (
                    PARTITION BY e.event_id
                    ORDER BY e.time_stamp_utc DESC
                ) AS rn
            FROM @Events e
        )
        INSERT INTO #Src (event_id, customer_id, activity_type, time_stamp_utc, details_json, expected_row_version)
        SELECT
            event_id,
            customer_id,
            activity_type,
            time_stamp_utc,
            details_json,
            expected_row_version
        FROM Deduped
        WHERE rn = 1;

        CREATE TABLE #Conflicts
        (
            event_id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            expected_row_version   VARBINARY(8)     NULL,
            current_row_version    VARBINARY(8)     NOT NULL,
            current_time_stamp_utc DATETIME2(3)     NOT NULL
        );

        INSERT INTO #Conflicts(event_id, expected_row_version, current_row_version, current_time_stamp_utc)
        SELECT
            tgt.event_id,
            src.expected_row_version,
            tgt.row_version,
            tgt.time_stamp_utc
        FROM dbo.CustomerActivityEvents AS tgt
        INNER JOIN #Src AS src
            ON src.event_id = tgt.event_id
        WHERE src.expected_row_version IS NOT NULL
          AND tgt.row_version <> src.expected_row_version;

        DECLARE @RowsConflicted INT = @@ROWCOUNT;

        UPDATE tgt
        SET
            tgt.customer_id    = src.customer_id,
            tgt.activity_type  = src.activity_type,
            tgt.time_stamp_utc = src.time_stamp_utc,
            tgt.details_json   = src.details_json
        FROM dbo.CustomerActivityEvents AS tgt
        INNER JOIN #Src AS src
            ON src.event_id = tgt.event_id
        WHERE tgt.time_stamp_utc < src.time_stamp_utc
          AND (src.expected_row_version IS NULL OR tgt.row_version = src.expected_row_version);

        DECLARE @RowsUpdated INT = @@ROWCOUNT;

        INSERT INTO dbo.CustomerActivityEvents
            (event_id, customer_id, activity_type, time_stamp_utc, details_json)
        SELECT
            src.event_id,
            src.customer_id,
            src.activity_type,
            src.time_stamp_utc,
            src.details_json
        FROM #Src AS src
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.CustomerActivityEvents WITH (UPDLOCK, HOLDLOCK)
            WHERE event_id = src.event_id
        );

        DECLARE @RowsInserted INT = @@ROWCOUNT;

        COMMIT;

        SELECT
            @RowsUpdated    AS RowsUpdated,
            @RowsInserted   AS RowsInserted,
            @RowsConflicted AS RowsConflicted;

        SELECT
            event_id,
            expected_row_version,
            current_row_version,
            current_time_stamp_utc
        FROM #Conflicts
        ORDER BY event_id;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;

        THROW;
    END CATCH
END;
GO

IF OBJECT_ID('dbo.CustomerActivityEvents_Staging', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerActivityEvents_Staging
    (
        event_id            UNIQUEIDENTIFIER    NOT NULL,
        customer_id         INT                 NOT NULL,
        activity_type       NVARCHAR(50)        NOT NULL,
        time_stamp_utc      DATETIME2(3)        NOT NULL,
        details_json        NVARCHAR(MAX)       NULL,

        batch_id            UNIQUEIDENTIFIER    NOT NULL,
        loaded_utc          DATETIME2(3)         NOT NULL CONSTRAINT DF_Staging_LoadedUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF COL_LENGTH('dbo.CustomerActivityEvents_Staging', 'expected_row_version') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerActivityEvents_Staging
    ADD expected_row_version VARBINARY(8) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CustomerActivityEvents_Staging')
      AND name = 'IX_Staging_BatchId_EventId'
)
BEGIN
    CREATE INDEX IX_Staging_BatchId_EventId
    ON dbo.CustomerActivityEvents_Staging (batch_id, event_id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CustomerActivityEvents_Staging')
      AND name = 'IX_CAES_Staging_LoadedUtc'
)
BEGIN
    CREATE INDEX IX_CAES_Staging_LoadedUtc
    ON dbo.CustomerActivityEvents_Staging(loaded_utc);
END
GO

CREATE OR ALTER PROCEDURE dbo.ReconcileCustomerActivityEventsBatch
    @BatchId UNIQUEIDENTIFIER,
    @RowsUpdated    INT OUTPUT,
    @RowsInserted   INT OUTPUT,
    @RowsConflicted INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        CREATE TABLE #Src
        (
            event_id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            customer_id           INT              NOT NULL,
            activity_type         NVARCHAR(50)     NOT NULL,
            time_stamp_utc        DATETIME2(3)     NOT NULL,
            details_json          NVARCHAR(MAX)    NULL,
            expected_row_version  VARBINARY(8)     NULL
        );

        ;WITH Deduped AS
        (
            SELECT
                stg.event_id,
                stg.customer_id,
                stg.activity_type,
                stg.time_stamp_utc,
                stg.details_json,
                stg.expected_row_version,
                ROW_NUMBER() OVER
                (
                    PARTITION BY stg.event_id
                    ORDER BY stg.time_stamp_utc DESC, stg.loaded_utc DESC
                ) AS rn
            FROM dbo.CustomerActivityEvents_Staging AS stg WITH (READPAST)
            WHERE stg.batch_id = @BatchId
        )
        INSERT INTO #Src (event_id, customer_id, activity_type, time_stamp_utc, details_json, expected_row_version)
        SELECT
            event_id, customer_id, activity_type, time_stamp_utc, details_json, expected_row_version
        FROM Deduped
        WHERE rn = 1;

        CREATE TABLE #Conflicts
        (
            event_id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            expected_row_version   VARBINARY(8)     NULL,
            current_row_version    VARBINARY(8)     NOT NULL,
            current_time_stamp_utc DATETIME2(3)     NOT NULL
        );

        INSERT INTO #Conflicts(event_id, expected_row_version, current_row_version, current_time_stamp_utc)
        SELECT
            tgt.event_id,
            src.expected_row_version,
            tgt.row_version,
            tgt.time_stamp_utc
        FROM dbo.CustomerActivityEvents AS tgt
        INNER JOIN #Src AS src
            ON src.event_id = tgt.event_id
        WHERE src.expected_row_version IS NOT NULL
          AND tgt.row_version <> src.expected_row_version;

        SET @RowsConflicted = @@ROWCOUNT;

        UPDATE tgt
        SET
            tgt.customer_id    = src.customer_id,
            tgt.activity_type  = src.activity_type,
            tgt.time_stamp_utc = src.time_stamp_utc,
            tgt.details_json   = src.details_json
        FROM dbo.CustomerActivityEvents AS tgt
        INNER JOIN #Src AS src
            ON src.event_id = tgt.event_id
        WHERE tgt.time_stamp_utc < src.time_stamp_utc
          AND (src.expected_row_version IS NULL OR tgt.row_version = src.expected_row_version);

        SET @RowsUpdated = @@ROWCOUNT;

        INSERT INTO dbo.CustomerActivityEvents
            (event_id, customer_id, activity_type, time_stamp_utc, details_json)
        SELECT
            src.event_id,
            src.customer_id,
            src.activity_type,
            src.time_stamp_utc,
            src.details_json
        FROM #Src AS src
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.CustomerActivityEvents WITH (UPDLOCK, HOLDLOCK)
            WHERE event_id = src.event_id
        );

        SET @RowsInserted = @@ROWCOUNT;

        DELETE FROM dbo.CustomerActivityEvents_Staging
        WHERE batch_id = @BatchId;

        COMMIT;


        SELECT
            event_id,
            expected_row_version,
            current_row_version,
            current_time_stamp_utc
        FROM #Conflicts
        ORDER BY event_id;

    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO