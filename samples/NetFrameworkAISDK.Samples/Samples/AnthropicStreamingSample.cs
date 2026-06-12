using NetFrameworkAISDK.Anthropic;
using NetFrameworkAISDK.Common;
using System;

namespace NetFrameworkAISDK.Samples
{
    public class AnthropicStreamingSample : ISample
    {
        public string Name
        {
            get { return "Anthropic - Streaming Message"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates streaming message with Anthropic AIAgent.");
            Console.WriteLine("----------------------------------------------------------");
            
            var config = SampleConfig.ReadFromConsole("Anthropic", "https://api.anthropic.com/v1", 
                "claude-3-sonnet-20240229", 1024, null, true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Using configuration:");
            Console.WriteLine("- API Key: " + SampleConfig.MaskKey(config.ApiKey));
            Console.WriteLine("- Base URL: " + config.BaseUrl);
            Console.WriteLine("- Model: " + config.Model);
            Console.WriteLine("- Max Tokens: " + config.MaxTokens);
            Console.WriteLine("- Temperature: " + (config.Temperature.HasValue ? config.Temperature.Value.ToString() : "(default)"));
            if (!string.IsNullOrEmpty(config.SystemPrompt))
            {
                Console.WriteLine("- System Prompt: " + config.SystemPrompt);
            }

            try
            {
                AnthropicClient client;
                if (!string.IsNullOrEmpty(config.BaseUrl))
                {
                    client = new AnthropicClient(config.ApiKey, config.BaseUrl);
                }
                else
                {
                    client = new AnthropicClient(config.ApiKey);
                }

                string instructions = !string.IsNullOrEmpty(config.SystemPrompt)
                    ? config.SystemPrompt
                    : "You are a helpful assistant.";

                var agent = new AIAgent(client, config.Model, instructions, null);

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                string userInput = Console.ReadLine();

                while (userInput != "exit")
                {
                    Console.Write("\nAssistant: ");

                    agent.RunStreaming(
                        userInput,
                        onUpdate: chunk => Console.Write(chunk),
                        onError: error => Console.WriteLine("\nError: " + error.Message)
                    );

                    Console.WriteLine("\n\nEnter your message (type 'exit' to quit):");
                    Console.Write("You: ");
                    userInput = Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nError: " + ex.Message);
            }
        }

    }
}
