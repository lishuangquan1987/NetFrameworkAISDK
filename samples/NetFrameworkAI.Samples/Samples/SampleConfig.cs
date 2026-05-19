using System;

namespace NetFrameworkAI.Samples
{
    public class SampleConfig
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public int MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public string SystemPrompt { get; set; }
        public bool HasValidConfig
        {
            get { return !string.IsNullOrEmpty(ApiKey); }
        }

        public static SampleConfig ReadFromConsole(string provider, string defaultUrl, string defaultModel, 
            int defaultMaxTokens = 1024, double? defaultTemperature = null, bool includeSystemPrompt = false)
        {
            Console.WriteLine();
            Console.WriteLine("Configuration for " + provider + ":");
            Console.WriteLine("-" + new string('-', 40));

            string apiKey = ReadString("API Key (required)", "");
            if (string.IsNullOrEmpty(apiKey))
            {
                return new SampleConfig { ApiKey = "" };
            }

            string baseUrl = ReadString("Base URL (optional, press Enter for default)", defaultUrl);
            string model = ReadString("Model (optional, press Enter for default)", defaultModel);
            int maxTokens = ReadInt("Max Tokens (optional, press Enter for default)", defaultMaxTokens);
            double? temperature = ReadNullableDouble("Temperature (optional, press Enter for default)", defaultTemperature);
            string systemPrompt = includeSystemPrompt 
                ? ReadString("System Prompt (optional, press Enter for none)", "") 
                : "";

            return new SampleConfig
            {
                ApiKey = apiKey,
                BaseUrl = baseUrl,
                Model = model,
                MaxTokens = maxTokens,
                Temperature = temperature,
                SystemPrompt = systemPrompt
            };
        }

        private static string ReadString(string prompt, string defaultValue)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write(prompt + " [" + defaultValue + "]: ");
            }
            else
            {
                Console.Write(prompt + ": ");
            }

            string input = Console.ReadLine();

            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            return input;
        }

        private static int ReadInt(string prompt, int defaultValue)
        {
            string input = ReadString(prompt, defaultValue.ToString());
            int result;
            if (int.TryParse(input, out result))
            {
                return result;
            }
            return defaultValue;
        }

        private static double? ReadNullableDouble(string prompt, double? defaultValue)
        {
            string input = ReadString(prompt, defaultValue.HasValue ? defaultValue.Value.ToString() : "");
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }
            double result;
            if (double.TryParse(input, out result))
            {
                return result;
            }
            return defaultValue;
        }
    }
}
