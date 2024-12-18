using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;

class Program
{
    static void Main(string[] args)
    {
        // Step 1: Setup Configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>();

        var configuration = builder.Build();

        // Step 2: Configure OpenAI using settings from appsettings.json
        string endpoint = configuration["AzureOpenAI:Endpoint"];
        string deploymentName = configuration["AzureOpenAI:DeploymentName"];
        string openAiApiKey = configuration["AzureOpenAI:ApiKey"];

        AzureKeyCredential credential = new(openAiApiKey);
        AzureOpenAIClient azureClient = new(new Uri(endpoint), credential);
        ChatClient chatClient = azureClient.GetChatClient(deploymentName);

        // Step 3: Configure Search Service using settings from appsettings.json
        string searchEndpoint = configuration["AzureSearch:Endpoint"];
        string searchIndex = configuration["AzureSearch:IndexName"];
        string searchKey = configuration["AzureSearch:ApiKey"];

        SearchClient searchClient = new SearchClient(new Uri(searchEndpoint), searchIndex, new AzureKeyCredential(searchKey));

        // Step 4: Query Azure AI Search Service
        Console.WriteLine("Hi there! I'm Harper Assist, please ask a question!");
        string userQuery = Console.ReadLine();
        SearchOptions searchOptions = new SearchOptions
        {
            IncludeTotalCount = true,
            QueryType = SearchQueryType.Full
        };
        SearchResults<SearchDocument> searchResults = searchClient.Search<SearchDocument>(userQuery, searchOptions);

        // Step 5: Combine search results into context and collect citations
        var searchContent = new List<string>();
        var citations = new List<string>();
        int citationIndex = 1;

        foreach (var result in searchResults.GetResults())
        {
            var content = result.Document["content"].ToString();
            var title = result.Document.ContainsKey("title") ? result.Document["title"].ToString() : "Unknown Source";
            var citation = $"[{citationIndex}] {title}";
            searchContent.Add($"{content} ({citation})");
            citations.Add(citation);
            citationIndex++;
        }
        string combinedContext = string.Join("\n", searchContent);

        // Step 6: Pass search results to Azure OpenAI
        ChatCompletion completion = chatClient.CompleteChat(
            new List<ChatMessage>()
            {
                new UserChatMessage(userQuery),
                new AssistantChatMessage($"Here is some context based on our data:\n{combinedContext}")
            },
            new ChatCompletionOptions
            {
                Temperature = (float)0.7,
                TopP = (float)0.95,
                FrequencyPenalty = (float)0,
                PresencePenalty = (float)0,
                MaxOutputTokenCount = 500,
            }
        );
        var response = completion.Content[0].Text;

        // Step 7: Add inline citations
        var responseWithCitations = $"{response}\n\nCitations:";
        foreach (var citation in citations)
        {
            responseWithCitations += $"\n{citation}";
        }

        Console.WriteLine("Here is what I found based on your data:");
        Console.WriteLine(responseWithCitations);
    }
}
