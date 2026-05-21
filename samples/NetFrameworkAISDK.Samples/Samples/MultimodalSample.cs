using NetFrameworkAISDK.OpenAI;
using NetFrameworkAISDK.Anthropic;
using NetFrameworkAISDK.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace NetFrameworkAISDK.Samples
{
    public class MultimodalSample : ISample
    {
        public string Name
        {
            get { return "Multimodal Chat - Image + Text Input with AIAgent"; }
        }

        public void Run()
        {
            Console.WriteLine("\nThis sample demonstrates multimodal (text + image) chat via AIAgent.");
            Console.WriteLine("Supports both OpenAI (gpt-4o) and Anthropic (claude-3-5-sonnet)!");
            Console.WriteLine("----------------------------------------------------------------------");

            var apiType = SelectProvider();

            if (apiType == "openai")
            {
                RunOpenAIMultimodal();
            }
            else if (apiType == "anthropic")
            {
                RunAnthropicMultimodal();
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }
        }

        private string SelectProvider()
        {
            Console.WriteLine("\nSelect AI provider for multimodal chat:");
            Console.WriteLine("1. OpenAI (gpt-4o)");
            Console.WriteLine("2. Anthropic (claude-3-5-sonnet)");
            Console.Write("Enter choice (1 or 2): ");
            var choice = Console.ReadLine();

            if (choice == "1") return "openai";
            if (choice == "2") return "anthropic";
            return "";
        }

        private void RunOpenAIMultimodal()
        {
            var config = SampleConfig.ReadFromConsole("OpenAI", "https://api.openai.com/v1",
                "gpt-4o", 1024, null, true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Using configuration:");
            Console.WriteLine("- Provider: OpenAI");
            Console.WriteLine("- Model: " + config.Model);
            Console.WriteLine("- API Key: " + SampleConfig.MaskKey(config.ApiKey));

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
                    "You are a helpful assistant. When the user provides an image, describe what you see."
                );

                RunInteractiveLoop(agent);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void RunAnthropicMultimodal()
        {
            var config = SampleConfig.ReadFromConsole("Anthropic", "https://api.anthropic.com/v1",
                "claude-3-5-sonnet-20241022", 1024, null, true);
            if (!config.HasValidConfig)
            {
                Console.WriteLine("API key is required. Skipping sample.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Using configuration:");
            Console.WriteLine("- Provider: Anthropic");
            Console.WriteLine("- Model: " + config.Model);
            Console.WriteLine("- API Key: " + SampleConfig.MaskKey(config.ApiKey));

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
                    "You are a helpful assistant. When the user provides an image, describe what you see."
                );

                RunInteractiveLoop(agent);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void RunInteractiveLoop(AIAgent agent)
        {
            Console.WriteLine("\n=== Multimodal Chat ===");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Plain text  : Just type your message and press Enter");
            Console.WriteLine("  With image  : Type /image <url> or /image64 <mediaType> <base64data>");
            Console.WriteLine("                Then type your text message");
            Console.WriteLine("  Exit        : Type 'exit'");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();

                if (input == null || input.ToLower() == "exit")
                {
                    Console.WriteLine("Bye!");
                    break;
                }

                if (input.StartsWith("/image "))
                {
                    var imageUrl = input.Substring("/image ".Length).Trim();
                    HandleImageChat(agent, imageUrl);
                }
                else if (input.StartsWith("/image64 "))
                {
                    var args = input.Substring("/image64 ".Length).Trim();
                    var spaceIndex = args.IndexOf(' ');
                    if (spaceIndex > 0)
                    {
                        var mediaType = args.Substring(0, spaceIndex);
                        var base64Data = args.Substring(spaceIndex + 1);
                        HandleImageBase64Chat(agent, base64Data, mediaType);
                    }
                    else
                    {
                        Console.WriteLine("Usage: /image64 <mediaType> <base64data>");
                    }
                }
                else
                {
                    HandleTextChat(agent, input);
                }
            }
        }

        private void HandleTextChat(AIAgent agent, string message)
        {
            Console.WriteLine("Assistant: ", false);
            Console.Write("Assistant: ");

            var typed = false;
            agent.RunStreaming(message, null, new Action<string>(text =>
            {
                Console.Write(text);
                typed = true;
            }), new Action<ApiError>(error =>
            {
                Console.WriteLine("\nError: " + error.Message);
            }));

            if (!typed)
            {
                Console.WriteLine("(no response)");
            }

            Console.WriteLine();
            Console.WriteLine();
        }

        private void HandleImageChat(AIAgent agent, string imageUrl)
        {
            Console.Write("Describe this image (or type your question): ");
            var question = Console.ReadLine();

            var contentParts = new List<MessageContent>
            {
                MessageContent.CreateImageFromUrl(imageUrl, "auto")
            };

            Console.WriteLine("Assistant: ", false);
            Console.Write("Assistant: ");

            var typed = false;
            agent.RunStreaming(question, contentParts, new Action<string>(text =>
            {
                Console.Write(text);
                typed = true;
            }), new Action<ApiError>(error =>
            {
                Console.WriteLine("\nError: " + error.Message);
            }));

            if (!typed)
            {
                Console.WriteLine("(no response)");
            }

            Console.WriteLine();
            Console.WriteLine();
        }

        private void HandleImageBase64Chat(AIAgent agent, string base64Data, string mediaType)
        {
            Console.Write("Describe this image (or type your question): ");
            var question = Console.ReadLine();

            var contentParts = new List<MessageContent>
            {
                MessageContent.CreateImageFromBase64(base64Data, mediaType)
            };

            Console.WriteLine("Assistant: ", false);
            Console.Write("Assistant: ");

            var typed = false;
            agent.RunStreaming(question, contentParts, new Action<string>(text =>
            {
                Console.Write(text);
                typed = true;
            }), new Action<ApiError>(error =>
            {
                Console.WriteLine("\nError: " + error.Message);
            }));

            if (!typed)
            {
                Console.WriteLine("(no response)");
            }

            Console.WriteLine();
            Console.WriteLine();
        }
    }
}