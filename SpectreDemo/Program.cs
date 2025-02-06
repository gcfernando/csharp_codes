using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

while (true)
{
    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[bold green]🚀 Starting Data Collection Process...[/]");

    const string developerName = "[italic yellow]👨\u200D💻 Developer ::> Gehan Fernando[/]";
    AnsiConsole.MarkupLine(developerName);

    // Live Log Panel
    AnsiConsole.Live(new Panel("[bold yellow]⏳ Initializing...[/]").Expand())
        .Start(ctx =>
        {
            ctx.UpdateTarget(new Panel("[cyan]🔹 Connecting to Server... 🌐[/]").Expand());
            Thread.Sleep(1000);

            ctx.UpdateTarget(new Panel("[blue]🔹 Fetching Data... 📡[/]").Expand());
            Thread.Sleep(1500);

            ctx.UpdateTarget(new Panel("[magenta]🔹 Processing Data... ⚡[/]").Expand());
            Thread.Sleep(2000);

            ctx.UpdateTarget(new Panel("[green]✅ Data Collection Complete! 🎉[/]").Expand());
            Thread.Sleep(1000);
        });

    // Progress Bar with Multiple Tasks
    AnsiConsole.MarkupLine("\n[bold yellow]⏳ Running Multiple Data Collection Tasks...[/]");

    AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new SpinnerColumn(),
            new RemainingTimeColumn())
        .Start(ctx =>
        {
            var task1 = ctx.AddTask("[green]📝 Fetching User Data[/]");
            var task2 = ctx.AddTask("[blue]📥 Downloading Reports[/]");
            var task3 = ctx.AddTask("[magenta]📊 Analyzing Logs[/]");

            while (!ctx.IsFinished)
            {
                task1.Increment(10);
                task2.Increment(7);
                task3.Increment(5);
                Thread.Sleep(500);
            }
        });

    AnsiConsole.MarkupLine("\n[bold green]✅ All Tasks Completed Successfully! 🎉[/]");

    // Restart Prompt
    var restart = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold yellow]🔄 Do you want to start again?[/]")
            .AddChoices("Yes", "No"));

    if (restart == "No")
    {
        AnsiConsole.MarkupLine("[bold red]👋 Exiting... Have a great day![/]");
        break;
    }
}