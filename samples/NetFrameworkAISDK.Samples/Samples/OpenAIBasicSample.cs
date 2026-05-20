using NetFrameworkAISDK.OpenAI;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Samples
{
    public class OpenAIBasicSample : ISample
    {
        public string Name
        {
            get { return "OpenAI - Basic Chat (Non-streaming)"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates basic non-streaming chat with OpenAI.");
            Console.WriteLine("----------------------------------------------------------");
            
            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1", "gpt-3.5-turbo");
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Using configuration:");
            Console.WriteLine("- API Key: " + MaskKey(config.ApiKey));
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
                    
                    var response = client.CreateChatCompletion(config.Model, messages);

                    if (response.IsSuccess)
                    {
                        Console.WriteLine(response.Result.Choices[0].Message.Content);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.Error.Message);
                    }

                    Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                    Console.Write("You: ");
                    userInput = Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nError: " + ex.Message);
            }
        }

        private string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "(empty)";
            }
            if (key.Length <= 8)
            {
                return new string('*', key.Length);
            }
            return key.Substring(0, 4) + "..." + key.Substring(key.Length - 4);
        }
    }
}
