using Newtonsoft.Json.Linq;

namespace JsonSearchPatterns;
internal static class FileHandler
{
    internal static List<JToken> Search(string pattern)
    {
        var jsonObject = ReadData();
        return jsonObject.SelectTokens(pattern).ToList();
    }

    private static JObject ReadData() =>
        JObject.Parse(File.ReadAllText("Data.json"));
}