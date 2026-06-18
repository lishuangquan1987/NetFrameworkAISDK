using NetFrameworkAISDK.Common;
using NetFrameworkAISDK.OpenAI;

string dbPath = "E:\\Yofc\\Code\\OTDR\\OTDR3001\\YOFC.OTDR3001\\YOFC.OTDR3001\\bin\\Debug\\netcoreapp3.1-windows\\win-x64\\TestData\\otdr_data.db";
string url = "https://u701357-b42c-d29bc5d1.westc.seetacloud.com:8443/v1";
string model = "Qwen3.6-35B-A3B-FP8";
string apiKey = "test";

string exePath = Path.Combine(AppContext.BaseDirectory, "dbmcp.exe");

// MCP 连接（自动完成初始化握手）
var mcpClient = new McpClient();
var connectResult = mcpClient.Connect(exePath, "stdio --db-backend sqlite --db-name " + dbPath);
if (!connectResult.IsSuccess)
{
    Console.WriteLine("连接出错：" + connectResult.Error.Message);
    return;
}

// 获取可用工具
var functionsResult = mcpClient.ListAsAIFunctions();
if (!functionsResult.IsSuccess)
{
    Console.WriteLine("获取工具出错：" + functionsResult.Error.Message);
    return;
}

Console.WriteLine("已发现 " + functionsResult.Result.Count + " 个工具");

// 注入 AI Agent
var client = new OpenAIClient(apiKey, url);
var agent = new AIAgent(client, model, "你是一个数据库专家，专为用户查询用户想要的数据", functionsResult.Result);

while (true)
{
    Console.WriteLine("请输入问题：");
    var question = Console.ReadLine();
    if (string.IsNullOrEmpty(question)) break;
    var answer = agent.Run(question);
    Console.WriteLine(answer.IsSuccess ? answer.Result : "错误：" + answer.Error.Message);
}
