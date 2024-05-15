using Fake.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Fake.Test;

[TestClass]
public class CalculateTest
{
    [TestMethod]
    public void CanDevide()
    {
        // Arrange
        var logger = new FakeLogger<Program>();
        var math = new Calculate(logger);

        // Act
        _ = math.Divide(10, 2);

        var last = logger.LatestRecord;

        // Assert
        Assert.IsTrue(last.Message.Contains("Dividing answer 5"));
    }

    [TestMethod]
    public void CanLogError()
    {
        // Arrange
        var logger = new FakeLogger<Program>();
        var math = new Calculate(logger);

        // Act
        _ = math.Divide(10, 0);

        var last = logger.LatestRecord;

        // Assert
        Assert.AreEqual(LogLevel.Error, last.Level);
    }
}