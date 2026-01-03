using Microsoft.Data.SqlClient;

namespace Common;

public static class SqlRetryPolicy
{
    public static async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct,
        int maxRetries = 5,
        int baseDelayMs = 100,
        int maxDelayMs = 5_000,
        Action<string> log = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDelayMs);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelayMs, baseDelayMs);

        var attempt = 0;
        var rng = Random.Shared;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await action(ct);
                return;
            }
            catch (SqlException ex) when (IsTransient(ex) && attempt < maxRetries)
            {
                attempt++;
                var backoffMs = baseDelayMs * Math.Pow(2, attempt - 1);
                var jitteredMs = backoffMs * (0.75 + (rng.NextDouble() * 0.5));
                var delayMs = (int)Math.Min(maxDelayMs, jitteredMs);
                log?.Invoke(
                    $"[SQL RETRY] attempt={attempt}/{maxRetries}, " +
                    $"delayMs={delayMs}, sqlError={ex.Number}, msg={ex.Message}");
                await Task.Delay(delayMs, ct);
            }
        }
    }

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        int maxRetries = 5)
    {
        ArgumentNullException.ThrowIfNull(action);
        Exception last = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
            {
                last = ex;
                await Task.Delay(ComputeDelay(attempt), cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException("Retry failed with unknown error.");

        static TimeSpan ComputeDelay(int attempt) => TimeSpan.FromMilliseconds(200 * attempt);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is SqlException sqlException)
        {
            foreach (SqlError err in sqlException.Errors)
            {
                switch (err.Number)
                {
                    case 1205:
                    case -2:
                    case 4060:
                    case 40197:
                    case 40501:
                    case 40613:
                    case 49918:
                    case 49919:
                    case 49920:
                    case 10053:
                    case 10054:
                    case 10060:
                        return true;
                }
            }

            return false;
        }

        return ex is TimeoutException || ex.InnerException is IOException;
    }
}