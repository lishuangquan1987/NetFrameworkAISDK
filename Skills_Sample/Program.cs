// See https://aka.ms/new-console-template for more information
using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;
using System.ComponentModel;

Console.WriteLine("Hello, World!");

HttpClientBase.ForceTlsProxyForDiagnostics();

string url = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
string model = "Qwen3.6-35B-A3B-FP8";
string apiKey = "test";

[Description("BBB运算")]
static double BBB([Description("BBB运算所需要的数字")]double number)
{
    return number * number;
}
var client = new OpenAIClient(apiKey, url);
var agent = new AIAgent(client, model, "你是一个计算专家，负责计算用户提出的问题", new List<AIFunction>()
{
    AIFunctionFactory.Create(BBB)
}, 
false, 
new string[]
{
    "./skillFolder",
});

while (true)
{
    Console.WriteLine("请输入问题：");
    var question = Console.ReadLine();
    var answer = agent.Run(question);
    if (answer.IsSuccess)
    {
        Console.WriteLine(answer.Result);
    }
    else
    {
        Console.WriteLine("错误：" + answer.Error.Message);
    }
}