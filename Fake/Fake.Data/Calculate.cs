using Microsoft.Extensions.Logging;

namespace Fake.Data;
public class Calculate(ILogger<Program> logger)
{
    private readonly ILogger<Program> _logger = logger;

    public int? Divide(int dividend, int divisor)
    {
        try
        {
            _logger.LogInformation("Dividing {Dividend} with {Divisor}", dividend, divisor);
            var result = dividend / divisor;
            _logger.LogInformation("Dividing answer {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during divide");
            return null;
        }
    }
}