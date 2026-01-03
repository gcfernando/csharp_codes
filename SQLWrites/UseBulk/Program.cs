using Common;
using UseBulk;

var connectionString = "Data Source=HOME-LAP-01;Initial Catalog=Test;Integrated Security=True;Trust Server Certificate=True";

var writer = new CustomerActivityEventBulkStagingWriter(connectionString);

static IEnumerable<CustomerActivityEvent> StreamEvents()
{
    for (var i = 0; i < 200_000; i++)
    {
        yield return new CustomerActivityEvent(
            EventId: Guid.NewGuid(),
            CustomerId: 42,
            ActivityType: "ButtonClick",
            TimeStampUtc: DateTime.UtcNow.AddMilliseconds(i),
            DetailsJson: """{"button":"buy"}""",
            RowVersion: Array.Empty<byte>(),
            ExpectedRowVersion: Array.Empty<byte>()
        );
    }
}

var (rowsUpdated, rowsInserted, rowsConflicted) =
    await writer.UpsertViaStagingWithMetricsAsync(StreamEvents());

Console.WriteLine($"Updated={rowsUpdated}, Inserted={rowsInserted}, Conflicted={rowsConflicted}");