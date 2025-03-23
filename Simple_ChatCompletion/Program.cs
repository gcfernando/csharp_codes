using Simple_ChatCompletion.ConnectByPackage;
using Simple_ChatCompletion.ConnectByRest;

Console.Title = "Simple Chat Demo";
Console.ForegroundColor = ConsoleColor.DarkGreen;

var correctMessage = "Can you help me to find the values of Sealing, Matched arrangement in 6205?";
var wrongMessage = "What is the capital of Sri Lanka?";

Console.WriteLine("** Package Demo **");

var queryResult = await PackageDemo.FindProductAsync(correctMessage);
Console.WriteLine(queryResult);

queryResult = await PackageDemo.FindProductAsync(wrongMessage);
Console.WriteLine(queryResult);

Console.WriteLine("");
Console.WriteLine("** Rest Demo **");

queryResult = await RestDemo.FindProductAsync(correctMessage);
Console.WriteLine(queryResult);

queryResult = await RestDemo.FindProductAsync(wrongMessage);
Console.WriteLine(queryResult);

Console.Read();