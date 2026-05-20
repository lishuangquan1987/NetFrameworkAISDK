using NetFrameworkAISDK.OpenAI;
using System;
using System.Collections.Generic;

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
            Console.WriteLine("\nThis sample demonstrates streaming chat with OpenAI.");
            Console.WriteLine("-----------------------------------------------------");
            
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

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                string userInput = Console.ReadLine();

                while (userInput != "exit")
                {
                    var messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatRole.User, Content = userInput }
                    };

                    Console.Write("\nAssistant: ");
                    
                    client.CreateChatCompletionStream(
                        config.Model,
                        messages,
                        onData: streamResponse =>
                        {
                            if (streamResponse.Choices != null && streamResponse.Choices.Count > 0)
                            {
                                var delta = streamResponse.Choices[0].Delta;
                                if (delta != null && !string.IsNullOrEmpty(delta.Content))
                                {
                                    Console.Write(delta.Content);
                                }
                            }
                        },
                        onError: error =>
                        {
                            Console.WriteLine("\nError: " + error.Message);
                        }
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
