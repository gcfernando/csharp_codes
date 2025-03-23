namespace Simple_ChatCompletion;
public static class Configuration
{
    public const string EndPoint = "https://skf-openai-dev-eval.openai.azure.com/";
    public const string Key = "Ega0KDTFLpgBxgJJ3FGQNHX2bk3GDsYuRWvl1c4rzZoj4PMHD7vtJQQJ99ALACYeBjFXJ3w3AAABACOGisf1";
    public const string Model = "gpt-4o-mini";
    public const string Version = "2024-08-01-preview";

    public const string SystemMessage = "You are an AI assistant. Your task is to extract the product code—a unique identifier, "
                                        + "such as '6205' or 'ABC123'—from the user query, along with any product attributes mentioned. "
                                        + "Extract attributes exactly as they appear in the query. "
                                        + "Respond in JSON format as follows: {\"product\": \"<product_code or product_name>\", \"attributes\": [\"<attribute1>\", \"<attribute2>\", ...]}. "
                                        + "If no product code is present, set \"product\" to \"unknown product\". "
                                        + "If no attributes are mentioned, return an empty array for \"attributes\". "
                                        + "Do not modify, rephrase, or infer attributes; capture them exactly as stated in the query. "
                                        + "If the query is not related to a product, respond with: \"I am sorry, I cannot help with your request. Your question is not related to products.\"";
}
