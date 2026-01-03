using Common;
using UseTvp;

var connectionString = "Data Source=HOME-LAP-01;Initial Catalog=Test;Integrated Security=True;Trust Server Certificate=True";
var writer = new CustomerActivityEventTvpWriter(connectionString);

var batch = new List<CustomerActivityEvent>();
for (var i = 0; i < 2000; i++)
{
    batch.Add(new CustomerActivityEvent(
        EventId: Guid.NewGuid(),
        CustomerId: 42,
        ActivityType: "AppOpen",
        TimeStampUtc: DateTime.UtcNow,
        DetailsJson: """{"device":"iPhone","version":"1.2.3"}""",
        RowVersion: Array.Empty<byte>(),
        ExpectedRowVersion: Array.Empty<byte>()
    ));
}

await writer.UpsertBatchAsync(batch);
Console.WriteLine("TVP batch upsert done.");