using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Simple_ChatCompletion.ConnectByPackage;
public static class PackageDemo
{
    public static async Task<string> FindProductAsync(string query)
    {
        AzureOpenAIClient openAIClient = new(
            new Uri(Configuration.EndPoint),
            new ApiKeyCredential(Configuration.Key));

        var chatClient = openAIClient.GetChatClient(Configuration.Model);

        var option = new ChatCompletionOptions()
        {
            Temperature = 0.7f,
            MaxOutputTokenCount = 100
        };

        ChatCompletion completion = await chatClient.CompleteChatAsync([
            new SystemChatMessage(Configuration.SystemMessage),
            new UserChatMessage(query)], option);

        return completion.Content[0].Text;
    }
}
