namespace Common;

public sealed record CustomerActivityEvent(Guid EventId,
    int CustomerId,
    string ActivityType,
    DateTime TimeStampUtc,
    string DetailsJson, byte[]
    RowVersion, byte[]
    ExpectedRowVersion);