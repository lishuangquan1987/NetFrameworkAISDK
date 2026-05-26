using System;

namespace NetFrameworkAISDK.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  NetFrameworkAI SDK - Sample Applications");
            Console.WriteLine("========================================");
            Console.WriteLine();

            while (true)
            {
                ShowMenu();
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        RunSample(new OpenAIBasicSample());
                        break;
                    case "2":
                        RunSample(new OpenAIStreamingSample());
                        break;
                    case "3":
                        RunSample(new OpenAIToolCallSample());
                        break;
                    case "4":
                        RunSample(new AnthropicBasicSample());
                        break;
                    case "5":
                        RunSample(new AnthropicStreamingSample());
                        break;
                    case "6":
                        RunSample(new AnthropicAIAgentSample());
                        break;
                    case "7":
                        RunSample(new McpToolSample());
                        break;
                    case "8":
                        RunSample(new SkillsSample());
                        break;
                    case "9":
                        RunSample(new MultimodalSample());
                        break;
                    case "10":
                        RunSample(new StructuredOutputSample());
                        break;
                    case "0":
                        Console.WriteLine("\nExiting...");
                        return;
                    default:
                        Console.WriteLine("\nInvalid option. Please try again.\n");
                        break;
                }
            }
        }

        static void ShowMenu()
        {
            Console.WriteLine("Please select a sample to run:");
            Console.WriteLine("1. OpenAI - Basic Chat (Non-streaming)");
            Console.WriteLine("2. OpenAI - Streaming Chat");
            Console.WriteLine("3. OpenAI - Tool Calling with AIAgent");
            Console.WriteLine("4. Anthropic - Basic Message (Non-streaming)");
            Console.WriteLine("5. Anthropic - Streaming Message");
            Console.WriteLine("6. Anthropic - Tool Calling with AIAgent (NEW)");
            Console.WriteLine("7. MCP Tool Calling - Connect MCP Server + AIAgent");
            Console.WriteLine("8. Agent Skills - Discover and Load SKILL.md");
            Console.WriteLine("9. Multimodal Chat - Image + Text Input with AIAgent");
            Console.WriteLine("10. Structured Output - AI returns typed objects");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");
        }

        static void RunSample(ISample sample)
        {
            try
            {
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("Running: " + sample.Name);
                Console.WriteLine(new string('-', 50));
                sample.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nError: " + ex.Message);
            }
            finally
            {
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                Console.WriteLine("\n");
            }
        }
    }

}
