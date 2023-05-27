// Developer ::> Gehan Fernando

await MainMethodAsync();

static async Task MethodOneAsync()
{
    await Task.Delay(TimeSpan.FromSeconds(1));
    throw new Exception("Custom Error Method One");
}

static async Task MethodTwoAsync()
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    throw new Exception("Custom Error Method Two");
}

static async Task MainMethodAsync()
{
    var taskOne = MethodOneAsync();
    var taskTwo = MethodTwoAsync();

    var tasks = new Task[] { taskOne, taskTwo };

    try
    {
        await Task.WhenAll(taskOne, taskTwo);
    }
    catch
    {
        var exceptions = tasks.SelectMany(task => task.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>());

        foreach (var exception in exceptions)
        {
            Console.WriteLine(exception.Message);
        }
    }
}