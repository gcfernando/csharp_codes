// Developer ::> Gehan Fernando

using JsonSearchPatterns;
using Newtonsoft.Json.Linq;

List<JToken> items = null;

#region "Select all elements at the root level"
items = FileHandler.Search("$.*");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select all elements at the root level"

Console.WriteLine("\r\n");

#region "Select all elements of a specific property name at the root level"
items = FileHandler.Search("$.education");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select all elements of a specific property name at the root level"

Console.WriteLine("\r\n");

#region "Select all elements within an array at the root level"
items = FileHandler.Search("$.parents[*]");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select all elements within an array at the root level"

Console.WriteLine("\r\n");

#region "Select elements within an array that match a condition"
items = FileHandler.Search("$.parents[?(@.relationship == 'father')]");

foreach (var item in items)
{
    Console.WriteLine(item);
}

items = FileHandler.Search("$.parents[?(@.relationship == 'father' && @.age == 40)]");

foreach (var item in items)
{
    Console.WriteLine(item);
}

#endregion "Select elements within an array that match a condition"

Console.WriteLine("\r\n");

#region "Select all elements at any level within the JSON object"
items = FileHandler.Search("$..*");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select all elements at any level within the JSON object"

Console.WriteLine("\r\n");

#region "Select all elements with a specific property name at any level within the JSON object"
items = FileHandler.Search("$.communication.addresses.location[*]");

foreach (var item in items)
{
    Console.WriteLine(item);
}

items = FileHandler.Search("$..location");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select all elements with a specific property name at any level within the JSON object"

Console.WriteLine("\r\n");

#region "Select elements with a specific property name within an array"
items = FileHandler.Search("$.parents..relationship");

foreach (var item in items)
{
    Console.WriteLine(item);
}

items = FileHandler.Search("$..personal..number");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select all elements at any level within the JSON object"

Console.WriteLine("\r\n");

#region "Select the first element of an array"
items = FileHandler.Search("$.parents[0]");

foreach (var item in items)
{
    Console.WriteLine(item);
}

items = FileHandler.Search("$..location[0]");

foreach (var item in items)
{
    Console.WriteLine(item);
}

#endregion "Select the first element of an array"

Console.WriteLine("\r\n");

#region "Select elements based on a regular expression"
items = FileHandler.Search("$.parents[?(@.name =~ /^J/)]");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select elements based on a regular expression"

Console.WriteLine("\r\n");

#region "Select elements based on the presence of any of multiple properties"
items = FileHandler.Search("$.parents[?(@.occupation == 'Teacher' || @.age >= 40)]");

foreach (var item in items)
{
    Console.WriteLine(item);
}
#endregion "Select elements based on the presence of any of multiple properties"

Console.Read();
