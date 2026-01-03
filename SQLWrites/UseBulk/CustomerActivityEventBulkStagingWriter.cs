using System.Data;
using Common;
using Microsoft.Data.SqlClient;

namespace UseBulk;

public sealed class CustomerActivityEventBulkStagingWriter
{
    private readonly string _connectionString;
    private const int _bulkBatchSize = 50_000;
    private const int _bulkCopyTimeoutSeconds = 120;
    private const int _reconcileTimeoutSeconds = 180;
    private const int _maxRetries = 5;
    public CustomerActivityEventBulkStagingWriter(string connectionString)
        => _connectionString = connectionString;

    public Task<(int updated, int inserted, int rowsConflicted)> UpsertViaStagingAsync(
        IEnumerable<CustomerActivityEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        var batchId = Guid.NewGuid();

        return SqlRetryPolicy.ExecuteAsync(async ct =>
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            await BulkCopyToStagingAsync(connection, events, batchId, ct);
            return await ReconcileBatchAsync(connection, batchId, ct);
        }, cancellationToken, maxRetries: _maxRetries);
    }

    public Task<(int updated, int inserted, int rowsConflicted)> UpsertViaStagingWithMetricsAsync(
        IEnumerable<CustomerActivityEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        var batchId = Guid.NewGuid();
        return ExecuteWithMetricsAsync(events, batchId, cancellationToken);
    }

    private Task<(int updated, int inserted, int rowsConflicted)> ExecuteWithMetricsAsync(
        IEnumerable<CustomerActivityEvent> events,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        return SqlRetryPolicy.ExecuteAsync(async ct =>
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            await BulkCopyToStagingAsync(connection, events, batchId, ct);

            return await ReconcileBatchAsync(connection, batchId, ct);
        }, cancellationToken, maxRetries: _maxRetries);
    }

    private static async Task BulkCopyToStagingAsync(
        SqlConnection connection,
        IEnumerable<CustomerActivityEvent> events,
        Guid batchId,
        CancellationToken ct)
    {
        using var bulkCopy = new SqlBulkCopy(
            connection,
            SqlBulkCopyOptions.TableLock |
            SqlBulkCopyOptions.KeepNulls |
            SqlBulkCopyOptions.UseInternalTransaction,
            externalTransaction: null)
        {
            DestinationTableName = "dbo.CustomerActivityEvents_Staging",
            BatchSize = _bulkBatchSize,
            BulkCopyTimeout = _bulkCopyTimeoutSeconds,
            EnableStreaming = true,
            NotifyAfter = _bulkBatchSize
        };

        _ = bulkCopy.ColumnMappings.Add("batch_id", "batch_id");
        _ = bulkCopy.ColumnMappings.Add("event_id", "event_id");
        _ = bulkCopy.ColumnMappings.Add("customer_id", "customer_id");
        _ = bulkCopy.ColumnMappings.Add("activity_type", "activity_type");
        _ = bulkCopy.ColumnMappings.Add("time_stamp_utc", "time_stamp_utc");
        _ = bulkCopy.ColumnMappings.Add("details_json", "details_json");
        _ = bulkCopy.ColumnMappings.Add("expected_row_version", "expected_row_version");

        using var reader = new CustomerActivityEventDataReader(events, batchId);

        await bulkCopy.WriteToServerAsync(reader, ct);
    }

    private static async Task<(int updated, int inserted, int rowsConflicted)> ReconcileBatchAsync(
        SqlConnection connection,
        Guid batchId,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("dbo.ReconcileCustomerActivityEventsBatch", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _reconcileTimeoutSeconds
        };

        _ = cmd.Parameters.Add(new SqlParameter("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId });

        var pUpdated = new SqlParameter("@RowsUpdated", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pInserted = new SqlParameter("@RowsInserted", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pConflicted = new SqlParameter("@RowsConflicted", SqlDbType.Int) { Direction = ParameterDirection.Output };

        _ = cmd.Parameters.Add(pUpdated);
        _ = cmd.Parameters.Add(pInserted);
        _ = cmd.Parameters.Add(pConflicted);

        _ = await cmd.ExecuteNonQueryAsync(ct);

        return (
            pUpdated.Value is DBNull ? 0 : Convert.ToInt32(pUpdated.Value),
            pInserted.Value is DBNull ? 0 : Convert.ToInt32(pInserted.Value),
            pConflicted.Value is DBNull ? 0 : Convert.ToInt32(pConflicted.Value)
        );
    }
}