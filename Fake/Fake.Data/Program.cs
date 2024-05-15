using Microsoft.Extensions.Logging;

namespace Fake.Data;
public class Program
{
    public static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Application started.\r\n");

        var calculator = new Calculate(logger);

        // Example usage of the Calculate class
        const int dividend = 10;
        const int divisor = 2;

        var result = calculator.Divide(dividend, divisor);

        if (result.HasValue)
        {
            logger.LogInformation("Result of division: {Result}", result);
        }
        else
        {
            logger.LogError("Division failed.");
        }

        logger.LogInformation("\r\n");
        logger.LogInformation("Application ended.");

        Console.Read();
    }
}