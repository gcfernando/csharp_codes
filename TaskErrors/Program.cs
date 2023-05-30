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
    var tasks = new List<Task>();

    try
    {
        var taskOne = MethodOneAsync();
        var taskTwo = MethodTwoAsync();

        tasks.Add(taskOne);
        tasks.Add(taskTwo);

        await Task.WhenAll(tasks.ToArray());
    }
    catch (Exception ex)
    {
        var exceptions = tasks.SelectMany(task => task.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>());

        if (exceptions?.Any() == true)
        {
            foreach (var exception in exceptions)
            {
                Console.WriteLine(exception.Message);
            }

            return;
        }

        Console.WriteLine(ex.Message);
    }
}
