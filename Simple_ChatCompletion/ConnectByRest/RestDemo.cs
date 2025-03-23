using System.Text;
using Newtonsoft.Json;

namespace Simple_ChatCompletion.ConnectByRest;
public static class RestDemo
{
    public static async Task<string> FindProductAsync(string query)
    {
        using var client = new HttpClient();
        var url = $"{Configuration.EndPoint}openai/deployments/{Configuration.Model}/chat/completions?api-version={Configuration.Version}";
        client.DefaultRequestHeaders.Add("api-key", Configuration.Key);

        var requestBody = new
        {
            messages = new[]
            {
                new
                    {
                        role = "system",
                        content = Configuration.SystemMessage
                    },
                new {
                        role = "user",
                        content = query }
                },
            temperature = 0.7,
            max_tokens = 100
        };

        var jsonBody = JsonConvert.SerializeObject(requestBody);
        HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);
        var result = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        dynamic jsonResponse = JsonConvert.DeserializeObject(result);
        return jsonResponse.choices[0].message.content;
    }
}
