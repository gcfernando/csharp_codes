using System.Data;
using Common;

namespace UseBulk;

internal sealed class CustomerActivityEventDataReader : IDataReader
{
    private readonly IEnumerator<CustomerActivityEvent> _enumerator;
    private readonly Guid _batchId;
    private CustomerActivityEvent _current;

    public CustomerActivityEventDataReader(IEnumerable<CustomerActivityEvent> source, Guid batchId)
    {
        ArgumentNullException.ThrowIfNull(source);
        _enumerator = source.GetEnumerator();

        _batchId = batchId;
        _current = default!;
    }

    public int FieldCount => 7;

    public bool Read()
    {
        if (!_enumerator.MoveNext())
        {
            return false;
        }

        _current = _enumerator.Current;
        return true;
    }

    public object GetValue(int i) => i switch
    {
        0 => _batchId,
        1 => _current.EventId,
        2 => _current.CustomerId,
        3 => _current.ActivityType,
        4 => _current.TimeStampUtc,
        5 => (object?)_current.DetailsJson ?? DBNull.Value,
        6 => (_current.ExpectedRowVersion is { Length: > 0 })
                ? _current.ExpectedRowVersion
                : DBNull.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(i), $"Invalid column index: {i}")
    };

    public string GetName(int i) => i switch
    {
        0 => "batch_id",
        1 => "event_id",
        2 => "customer_id",
        3 => "activity_type",
        4 => "time_stamp_utc",
        5 => "details_json",
        6 => "expected_row_version",
        _ => throw new ArgumentOutOfRangeException(nameof(i), $"Invalid column index: {i}")
    };

    public int GetOrdinal(string name) => name switch
    {
        "batch_id" => 0,
        "event_id" => 1,
        "customer_id" => 2,
        "activity_type" => 3,
        "time_stamp_utc" => 4,
        "details_json" => 5,
        "expected_row_version" => 6,
        _ => -1
    };

    public Type GetFieldType(int i) => i switch
    {
        0 => typeof(Guid),
        1 => typeof(Guid),
        2 => typeof(int),
        3 => typeof(string),
        4 => typeof(DateTime),
        5 => typeof(string),
        6 => typeof(byte[]),
        _ => throw new ArgumentOutOfRangeException(nameof(i), $"Invalid column index: {i}")
    };

    public bool IsDBNull(int i) => i switch
    {
        5 => _current.DetailsJson is null,
        6 => _current.ExpectedRowVersion is null || _current.ExpectedRowVersion.Length == 0,

        _ => false
    };

    public void Dispose() => _enumerator.Dispose();
    public void Close() => Dispose();
    public DataTable? GetSchemaTable() => null;
    public bool NextResult() => false;
    public int Depth => 0;
    public bool IsClosed => false;
    public int RecordsAffected => -1;
    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));
    public bool GetBoolean(int i) => (bool)GetValue(i);
    public byte GetByte(int i) => (byte)GetValue(i);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
        => throw new NotSupportedException();

    public char GetChar(int i) => (char)GetValue(i);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
        => throw new NotSupportedException();
    public IDataReader GetData(int i) => throw new NotSupportedException();
    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
    public decimal GetDecimal(int i) => (decimal)GetValue(i);
    public double GetDouble(int i) => (double)GetValue(i);
    public float GetFloat(int i) => (float)GetValue(i);
    public Guid GetGuid(int i) => (Guid)GetValue(i);
    public short GetInt16(int i) => (short)GetValue(i);
    public int GetInt32(int i) => (int)GetValue(i);
    public long GetInt64(int i) => (long)GetValue(i);
    public string GetString(int i) => (string)GetValue(i);
    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);

        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }
}