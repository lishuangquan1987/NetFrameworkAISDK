using NetFrameworkAI.Anthropic;
using System;
using System.Collections.Generic;

namespace NetFrameworkAI.Samples
{
    public class AnthropicStreamingSample : ISample
    {
        public string Name
        {
            get { return "Anthropic - Streaming Message"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates streaming message with Anthropic.");
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
            Console.WriteLine("- API Key: " + MaskKey(config.ApiKey));
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

                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                string userInput = Console.ReadLine();

                while (userInput != "exit")
                {
                    var messages = new List<AnthropicMessage>
                    {
                        new AnthropicMessage { Role = AnthropicRole.User, Content = userInput }
                    };

                    Console.Write("\nAssistant: ");
                    
                    client.CreateMessageStream(
                        config.Model,
                        messages,
                        config.MaxTokens,
                        onEvent: streamResponse =>
                        {
                            if (streamResponse.Delta != null && !string.IsNullOrEmpty(streamResponse.Delta.Text))
                            {
                                Console.Write(streamResponse.Delta.Text);
                            }
                        },
                        onError: error =>
                        {
                            Console.WriteLine("\nError: " + error.Message);
                        },
                        system: string.IsNullOrEmpty(config.SystemPrompt) ? null : config.SystemPrompt,
                        temperature: config.Temperature
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
