using Common;
using UseNormal;

var connectionString = "Data Source=HOME-LAP-01;Initial Catalog=Test;Integrated Security=True;Trust Server Certificate=True";
var writer = new CustomerActivityEventWriter(connectionString);

var customerActivityEvent = new CustomerActivityEvent(
    EventId: Guid.NewGuid(),
    CustomerId: 42,
    ActivityType: "AppOpen",
    TimeStampUtc: DateTime.UtcNow,
    DetailsJson: """{"device":"iPhone","version":"1.2.3"}""",
    RowVersion: Array.Empty<byte>(),
    ExpectedRowVersion: Array.Empty<byte>()
);

await writer.UpsertAsync(customerActivityEvent);
Console.WriteLine("Done.");