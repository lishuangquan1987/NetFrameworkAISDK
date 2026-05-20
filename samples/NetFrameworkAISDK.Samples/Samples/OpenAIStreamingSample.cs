using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System;

namespace NetFrameworkAISDK.Samples
{
    public class OpenAIStreamingSample : ISample
    {
        public string Name
        {
            get { return "OpenAI - Streaming Chat"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates streaming chat with OpenAI AIAgent.");
            Console.WriteLine("----------------------------------------------------------");

            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1", "gpt-3.5-turbo");
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

            try
            {
                OpenAIClient client;
                if (!string.IsNullOrEmpty(config.BaseUrl))
                {
                    client = new OpenAIClient(config.ApiKey, config.BaseUrl);
                }
                else
                {
                    client = new OpenAIClient(config.ApiKey);
                }

                var agent = new AIAgent(client, config.Model,
                    "You are a helpful assistant.", null);

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