using Microsoft.SemanticKernel;

Kernel kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(
        modelId: "gemma-3-27b-it-abliterated", // Adjust based on the model you're using
        apiKey: "", // LM Studio doesn't require an API key
        endpoint: new Uri("http://localhost:1234/v1/")
    )
    .Build();

// Define a chat loop
while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrEmpty(userInput))
    {
        break; // Exit the loop if the input is empty
    }

    // Invoke the kernel with the user input
    FunctionResult result = await kernel.InvokePromptAsync(userInput);
    // Print the result
    Console.WriteLine($"AI: {result}");
}