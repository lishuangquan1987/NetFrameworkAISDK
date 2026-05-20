using NetFrameworkAISDK.OpenAI;
using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace NetFrameworkAISDK.Samples
{
    public class OpenAIToolCallSample : ISample
    {
        public string Name
        {
            get { return "OpenAI - Tool Calling with AIAgent"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates tool calling with AIAgent (MAF style).");
            Console.WriteLine("---------------------------------------------------------------");
            
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

                var agent = new Common.AIAgent(
                    client,
                    config.Model,
                    instructions: "You are a helpful assistant that can use tools to get information.",
                    tools: new[] { AIFunctionFactory.Create(new Func<string, string>(GetWeather)), AIFunctionFactory.Create(new Func<string>(GetCurrentTime)) }
                );

                Console.WriteLine("\nAvailable tools:");
                Console.WriteLine("- GetWeather(location) - Get current weather for a location");
                Console.WriteLine("- GetCurrentTime() - Get the current time");
                Console.WriteLine("\nEnter your message (type 'exit' to quit):");
                Console.Write("You: ");
                string userInput = Console.ReadLine();

                while (userInput != "exit")
                {
                    Console.Write("\nAssistant: ");

                    var response = agent.Run(userInput, onToolCall: (name, args, result) =>
                    {
                        Console.WriteLine("\n>>> Calling tool: " + name);
                        Console.WriteLine(">>> Arguments: " + args);
                        Console.WriteLine(">>> Result: " + result);
                    });

                    if (response.IsSuccess)
                    {
                        Console.WriteLine(response.Result);
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

        [Description("Get the current weather for a given location.")]
        static string GetWeather([Description("The location to get weather for")] string location)
        {
            var weatherData = new Dictionary<string, string>
            {
                { "beijing", "Sunny, 25°C" },
                { "shanghai", "Cloudy, 22°C" },
                { "tokyo", "Rainy, 18°C" },
                { "new york", "Partly cloudy, 20°C" }
            };

            string lowerLocation = location.ToLower();
            if (weatherData.ContainsKey(lowerLocation))
            {
                return "The weather in " + location + " is: " + weatherData[lowerLocation];
            }
            return "Sorry, I don't have weather data for " + location;
        }

        [Description("Get the current date and time.")]
        static string GetCurrentTime()
        {
            return "Current time: " + DateTime.Now.ToString("F");
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