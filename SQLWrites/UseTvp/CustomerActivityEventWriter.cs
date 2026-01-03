using System.Data;
using Common;
using Microsoft.Data.SqlClient;

namespace UseTvp;

public sealed class CustomerActivityEventTvpWriter
{
    private readonly string _connectionString;
    private const int _commandTimeoutSeconds = 120;
    private const int _maxRetries = 5;
    public CustomerActivityEventTvpWriter(string connectionString)
        => _connectionString = connectionString;

    public Task UpsertBatchAsync(
        IReadOnlyList<CustomerActivityEvent> customerActivityEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customerActivityEvents);

        return customerActivityEvents.Count == 0
            ? Task.CompletedTask
            : SqlRetryPolicy.ExecuteAsync(async ct =>
            {
                var tvp = CreateTvp(customerActivityEvents);

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(ct);

                await using var cmd = new SqlCommand("dbo.UpsertCustomerActivityEventsType", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _commandTimeoutSeconds
                };

                var parameter = new SqlParameter("@Events", SqlDbType.Structured)
                {
                    TypeName = "dbo.CustomerActivityEventType",
                    Value = tvp
                };

                _ = cmd.Parameters.Add(parameter);
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }, cancellationToken, maxRetries: _maxRetries);
    }

    private static DataTable CreateTvp(IReadOnlyList<CustomerActivityEvent> events)
    {
        var dt = new DataTable();

        _ = dt.Columns.Add("event_id", typeof(Guid));
        _ = dt.Columns.Add("customer_id", typeof(int));
        _ = dt.Columns.Add("activity_type", typeof(string));
        _ = dt.Columns.Add("time_stamp_utc", typeof(DateTime));
        _ = dt.Columns.Add("details_json", typeof(string));
        _ = dt.Columns.Add("expected_row_version", typeof(byte[]));

        foreach (var e in events)
        {
            var row = dt.NewRow();
            row["event_id"] = e.EventId;
            row["customer_id"] = e.CustomerId;
            row["activity_type"] = e.ActivityType;
            row["time_stamp_utc"] = e.TimeStampUtc;

            row["details_json"] = (object)e.DetailsJson ?? DBNull.Value;

            row["expected_row_version"] =
                (e.ExpectedRowVersion is { Length: > 0 })
                    ? e.ExpectedRowVersion
                    : DBNull.Value;

            dt.Rows.Add(row);
        }

        return dt;
    }
}