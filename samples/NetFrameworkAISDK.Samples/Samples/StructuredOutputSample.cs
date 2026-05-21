using NetFrameworkAISDK.OpenAI;
using NetFrameworkAISDK.Anthropic;
using NetFrameworkAISDK.Common;
using System;
using System.ComponentModel;

namespace NetFrameworkAISDK.Samples
{
    public class WeatherInfo
    {
        public string City { get; set; }
        public double Temperature { get; set; }
        public string Condition { get; set; }
    }

    public class PersonInfo
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class StructuredOutputSample : ISample
    {
        public string Name
        {
            get { return "Structured Output - AI returns typed objects"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates structured output via AIAgent.");
            Console.WriteLine("The AI returns JSON matching your C# type, auto-deserialized!");
            Console.WriteLine("Works with both OpenAI (response_format) and Anthropic (tool-use hack).");
            Console.WriteLine("----------------------------------------------------------------------");

            var apiType = SelectProvider();

            if (apiType == "openai")
            {
                RunOpenAIStructuredOutput();
            }
            else if (apiType == "anthropic")
            {
                RunAnthropicStructuredOutput();
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }
        }

        private string SelectProvider()
        {
            Console.WriteLine("\nSelect AI provider:");
            Console.WriteLine("1. OpenAI (gpt-4o / gpt-4o-mini) - Uses native response_format");
            Console.WriteLine("2. Anthropic (claude-3-5-sonnet) - Uses tool-use hack");
            Console.Write("Enter choice (1 or 2): ");
            var choice = Console.ReadLine();

            if (choice == "1") return "openai";
            if (choice == "2") return "anthropic";
            return "";
        }

        private void RunOpenAIStructuredOutput()
        {
            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1",
                "gpt-4o-mini", 1024, null, true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Provider: OpenAI | Model: " + config.Model);

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

                var agent = AIAgent.CreateMinimal(
                    client,
                    config.Model,
                    "You are a helpful assistant. Always respond with precise structured data."
                );

                RunStructuredChat(agent);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void RunAnthropicStructuredOutput()
        {
            var config = SampleConfig.ReadFromConsole("Anthropic", "https://api.anthropic.com/v1",
                "claude-3-5-sonnet-20241022", 1024, null, true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Provider: Anthropic | Model: " + config.Model);

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

                var agent = AIAgent.CreateMinimal(
                    client,
                    config.Model,
                    "You are a helpful assistant. Always respond with precise structured data."
                );

                RunStructuredChat(agent);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void RunStructuredChat(AIAgent agent)
        {
            Console.WriteLine("\n=== Structured Output Demo ===");
            Console.WriteLine("Type 'weather' for weather info, 'person' for person info, 'exit' to quit.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();

                if (input == null || input.ToLower() == "exit")
                {
                    Console.WriteLine("Bye!");
                    break;
                }

                if (input.ToLower() == "weather")
                {
                    var result = agent.RunStructured<WeatherInfo>(
                        "Give me the current weather for Beijing, China");

                    if (result.IsSuccess)
                    {
                        Console.WriteLine("=== Typed Result (WeatherInfo) ===");
                        Console.WriteLine("City: " + result.Result.City);
                        Console.WriteLine("Temperature: " + result.Result.Temperature);
                        Console.WriteLine("Condition: " + result.Result.Condition);
                        Console.WriteLine("==============================");
                    }
                    else
                    {
                        Console.WriteLine("Error: " + result.Error.Message);
                    }
                }
                else if (input.ToLower() == "person")
                {
                    var result = agent.RunStructured<PersonInfo>(
                        "Create a fictional person named Alice");

                    if (result.IsSuccess)
                    {
                        Console.WriteLine("=== Typed Result (PersonInfo) ===");
                        Console.WriteLine("Name: " + result.Result.Name);
                        Console.WriteLine("Age: " + result.Result.Age);
                        Console.WriteLine("==============================");
                    }
                    else
                    {
                        Console.WriteLine("Error: " + result.Error.Message);
                    }
                }
                else
                {
                    var result = agent.Run(input);
                    if (result.IsSuccess)
                    {
                        Console.WriteLine("Agent: " + result.Result);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + result.Error.Message);
                    }
                }
            }
        }
    }
}