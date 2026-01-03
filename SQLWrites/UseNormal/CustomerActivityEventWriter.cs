using System.Data;
using System.Text;
using Common;
using Microsoft.Data.SqlClient;

namespace UseNormal;

public sealed class CustomerActivityEventWriter
{
    private readonly string _connectionString;
    private const int _commandTimeoutSeconds = 30;
    private const int _maxRetries = 5;
    public CustomerActivityEventWriter(string connectionString)
        => _connectionString = connectionString;

    public Task UpsertAsync(
        CustomerActivityEvent customerActivityEvent,
        CancellationToken cancellationToken = default)
    {
        return SqlRetryPolicy.ExecuteAsync(async ct =>
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            try
            {
                var sqlBuilder = new StringBuilder();

                _ = sqlBuilder.AppendLine("UPDATE dbo.CustomerActivityEvents");
                _ = sqlBuilder.AppendLine("SET");
                _ = sqlBuilder.AppendLine("    customer_id    = @customer_id,");
                _ = sqlBuilder.AppendLine("    activity_type  = @activity_type,");
                _ = sqlBuilder.AppendLine("    time_stamp_utc = @time_stamp_utc,");
                _ = sqlBuilder.AppendLine("    details_json   = @details_json");
                _ = sqlBuilder.AppendLine("WHERE");
                _ = sqlBuilder.AppendLine("    event_id = @event_id");
                _ = sqlBuilder.AppendLine("    AND time_stamp_utc < @time_stamp_utc");
                _ = sqlBuilder.AppendLine("    AND (");
                _ = sqlBuilder.AppendLine("         @expected_row_version IS NULL");
                _ = sqlBuilder.AppendLine("         OR row_version = @expected_row_version");
                _ = sqlBuilder.AppendLine("    );");

                await using var updateCommand =
                    new SqlCommand(sqlBuilder.ToString(), connection, (SqlTransaction)transaction)
                    {
                        CommandTimeout = _commandTimeoutSeconds
                    };

                AddParameters(updateCommand, customerActivityEvent);
                _ = await updateCommand.ExecuteNonQueryAsync(ct);

                _ = sqlBuilder.Clear();

                _ = sqlBuilder.AppendLine("INSERT INTO dbo.CustomerActivityEvents");
                _ = sqlBuilder.AppendLine("    (event_id, customer_id, activity_type, time_stamp_utc, details_json)");
                _ = sqlBuilder.AppendLine("SELECT");
                _ = sqlBuilder.AppendLine("    @event_id, @customer_id, @activity_type, @time_stamp_utc, @details_json");
                _ = sqlBuilder.AppendLine("WHERE");
                _ = sqlBuilder.AppendLine("    NOT EXISTS");
                _ = sqlBuilder.AppendLine("    (");
                _ = sqlBuilder.AppendLine("        SELECT 1");
                _ = sqlBuilder.AppendLine("        FROM dbo.CustomerActivityEvents WITH (UPDLOCK, HOLDLOCK)");
                _ = sqlBuilder.AppendLine("        WHERE event_id = @event_id");
                _ = sqlBuilder.AppendLine("    );");

                await using var insertCommand =
                    new SqlCommand(sqlBuilder.ToString(), connection, (SqlTransaction)transaction)
                    {
                        CommandTimeout = _commandTimeoutSeconds
                    };

                AddParameters(insertCommand, customerActivityEvent);
                _ = await insertCommand.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }, cancellationToken, maxRetries: _maxRetries);
    }

    public async Task UpsertManyAsync(
        IReadOnlyList<CustomerActivityEvent> customerActivityEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var customerActivityEvent in customerActivityEvents)
        {
            await UpsertAsync(customerActivityEvent, cancellationToken);
        }
    }

    private static void AddParameters(SqlCommand command, CustomerActivityEvent e)
    {
        _ = command.Parameters.Add(new SqlParameter("@event_id", SqlDbType.UniqueIdentifier) { Value = e.EventId });
        _ = command.Parameters.Add(new SqlParameter("@customer_id", SqlDbType.Int) { Value = e.CustomerId });
        _ = command.Parameters.Add(new SqlParameter("@activity_type", SqlDbType.NVarChar, 50) { Value = e.ActivityType });
        _ = command.Parameters.Add(new SqlParameter("@time_stamp_utc", SqlDbType.DateTime2) { Value = e.TimeStampUtc });

        _ = command.Parameters.Add(new SqlParameter("@details_json", SqlDbType.NVarChar, -1)
        {
            Value = (object)e.DetailsJson ?? DBNull.Value
        });

        _ = command.Parameters.Add(new SqlParameter("@expected_row_version", SqlDbType.VarBinary, 8)
        {
            Value = (e.ExpectedRowVersion is { Length: > 0 })
                ? e.ExpectedRowVersion
                : DBNull.Value
        });
    }
}