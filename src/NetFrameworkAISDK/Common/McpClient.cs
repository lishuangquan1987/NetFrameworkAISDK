using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP 客户端，通过子进程标准输入输出与 MCP 服务器通信。
    /// 一行连接并获取 AI 可用工具：Connect → ListAsAIFunctions。
    /// </summary>
    public class McpClient : IDisposable
    {
        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private int _requestId;
        private bool _initialized;
        private bool _disposed;
        private volatile bool _aborted;
        private int _timeoutMilliseconds;
        private readonly object _sendLock;
        private readonly ILogger _logger;

        /// <summary>
        /// 创建 MCP 客户端（默认 30 秒超时）
        /// </summary>
        public McpClient()
            : this(30000, null)
        {
        }

        /// <summary>
        /// 创建 MCP 客户端并指定超时时间
        /// </summary>
        public McpClient(int timeoutMilliseconds)
            : this(timeoutMilliseconds, null)
        {
        }

        /// <summary>
        /// 创建 MCP 客户端并指定超时时间和日志记录器
        /// </summary>
        public McpClient(int timeoutMilliseconds, ILogger logger)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
            _sendLock = new object();
            _logger = logger ?? new FileLogger();
        }

        /// <summary>是否已连接到 MCP 服务器</summary>
        public bool IsConnected
        {
            get { return _process != null && !_process.HasExited; }
        }

        /// <summary>
        /// 启动 MCP 服务器子进程并完成初始化握手
        /// </summary>
        /// <param name="serverPath">可执行文件路径（支持 PATH 查找）</param>
        /// <param name="arguments">命令行参数（可选）</param>
        /// <returns>连接结果</returns>
        public ApiResponse<bool> Connect(string serverPath, string arguments = null)
        {
            try
            {
                var psi = new ProcessStartInfo(serverPath, arguments ?? "")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _process = new Process { StartInfo = psi };
                _process.Start();

                try
                {
                    if (!_process.WaitForInputIdle(2000))
                    {
                        if (_process.HasExited)
                        {
                            int exitCode = _process.ExitCode;
                            _process.Dispose();
                            _process = null;
                            return new ApiResponse<bool> { Error = new ApiError("MCP server process exited immediately with code: " + exitCode) };
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // 控制台程序无 GUI 消息循环，忽略
                }

                _stdin = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false));
                _stdout = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);
                _requestId = 0;

                // 自动执行初始化握手
                var initResponse = SendRequest("initialize", new Dictionary<string, object>
                {
                    { "protocolVersion", "2025-03-26" },
                    { "capabilities", new Dictionary<string, object>() },
                    { "clientInfo", new Dictionary<string, object>
                        {
                            { "name", "NetFrameworkAI" },
                            { "version", "1.0.0" }
                        }
                    }
                });

                if (!initResponse.IsSuccess)
                {
                    // 初始化失败，清理进程
                    try { _process.Kill(); } catch { }
                    try { _process.Dispose(); } catch { }
                    _process = null;
                    return new ApiResponse<bool> { Error = initResponse.Error };
                }

                // MCP 协议：收到 initialize 响应后必须发送 initialized 通知
                SendNotification("notifications/initialized", new Dictionary<string, object>());
                _initialized = true;

                return new ApiResponse<bool> { Result = true };
            }
            catch (Exception ex)
            {
                if (_process != null)
                {
                    try { _process.Dispose(); } catch { }
                    _process = null;
                }
                return new ApiResponse<bool> { Error = new ApiError("Failed to connect MCP server: " + ex.Message) };
            }
        }

        /// <summary>
        /// 获取 MCP 工具列表并全部转换为 AIFunction，可直接注入 AIAgent
        /// </summary>
        public ApiResponse<List<AIFunction>> ListAsAIFunctions()
        {
            var toolsResult = ListTools();
            if (!toolsResult.IsSuccess)
                return new ApiResponse<List<AIFunction>> { Error = toolsResult.Error };

            var functions = new List<AIFunction>();
            var client = this;
            foreach (var tool in toolsResult.Result)
            {
                var toolName = tool.Name; // 捕获循环变量
                functions.Add(AIFunctionFactory.Create(
                    tool.Name, tool.Description, tool.InputSchema,
                    new Func<string, string>(args =>
                    {
                        var result = client.CallTool(toolName, args);
                        return result.IsSuccess ? result.Result : "Error: " + result.Error.Message;
                    })));
            }
            return new ApiResponse<List<AIFunction>> { Result = functions };
        }

        /// <summary>
        /// 释放所有资源（关闭管道和进程）
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_initialized && IsConnected)
            {
                try { SendRequest("shutdown", new Dictionary<string, object>()); }
                catch { }
            }
            _initialized = false;
            _aborted = true;

            if (_stdin != null) { try { _stdin.Close(); } catch { } }
            if (_stdout != null) { try { _stdout.Close(); } catch { } }
            if (_process != null)
            {
                try { if (!_process.HasExited) _process.Kill(); } catch { }
                try { _process.Dispose(); } catch { }
            }
        }

        // ============================================================
        // 内部方法
        // ============================================================

        private ApiResponse<List<McpToolInfo>> ListTools()
        {
            try
            {
                var response = SendRequest("tools/list", new Dictionary<string, object>());
                if (!response.IsSuccess)
                    return new ApiResponse<List<McpToolInfo>> { Error = response.Error };

                var result = response.Result;
                if (result == null)
                    return new ApiResponse<List<McpToolInfo>> { Error = new ApiError("MCP list_tools returned null") };

                var content = JsonHelper.Serialize(result);
                var toolsResult = JsonHelper.Deserialize<McpListToolsResult>(content);
                var tools = new List<McpToolInfo>();
                if (toolsResult != null && toolsResult.Tools != null)
                {
                    foreach (var t in toolsResult.Tools)
                        tools.Add(new McpToolInfo { Name = t.Name, Description = t.Description, InputSchema = t.InputSchema });
                }
                return new ApiResponse<List<McpToolInfo>> { Result = tools };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<McpToolInfo>> { Error = new ApiError("MCP list_tools failed: " + ex.Message) };
            }
        }

        private ApiResponse<string> CallTool(string toolName, object arguments)
        {
            try
            {
                object parsedArgs = arguments;
                if (arguments is string && !string.IsNullOrEmpty((string)arguments))
                {
                    try { parsedArgs = JsonHelper.Deserialize<object>((string)arguments); } catch { }
                }

                var response = SendRequest("tools/call", new Dictionary<string, object>
                {
                    { "name", toolName },
                    { "arguments", parsedArgs ?? new Dictionary<string, object>() }
                });

                if (!response.IsSuccess)
                    return new ApiResponse<string> { Error = response.Error };

                var result = response.Result;
                return new ApiResponse<string> { Result = result != null ? JsonHelper.Serialize(result) : "" };
            }
            catch (Exception ex)
            {
                return new ApiResponse<string> { Error = new ApiError("MCP call_tool failed: " + ex.Message) };
            }
        }

        private ApiResponse<object> SendRequest(string method, object parameters)
        {
            lock (_sendLock)
            {
                _aborted = false;
                _requestId++;

                var request = new Dictionary<string, object>
                {
                    { "jsonrpc", "2.0" },
                    { "id", _requestId },
                    { "method", method },
                    { "params", parameters }
                };

                string requestJson = JsonHelper.Serialize(request);
                _logger.Log(string.Format("MCP sending request: method={0}, id={1}", method, _requestId), "DEBUG");

                _stdin.WriteLine(requestJson);
                _stdin.Flush();

                string responseLine = ReadLineWithTimeout(_timeoutMilliseconds);
                if (responseLine == null)
                {
                    string errorMsg = string.Format("MCP request timed out after {0}ms", _timeoutMilliseconds);
                    _logger.Log(errorMsg, "WARN");
                    return new ApiResponse<object> { Error = new ApiError(errorMsg) };
                }

                var response = JsonHelper.Deserialize<McpJsonRpcResponse>(responseLine);
                if (response == null)
                {
                    string errorMsg = "Failed to parse MCP response";
                    _logger.Log(errorMsg, "ERROR");
                    return new ApiResponse<object> { Error = new ApiError(errorMsg) };
                }

                if (response.Error != null)
                {
                    string errorMsg = response.Error.Message ?? "MCP request error";
                    _logger.Log(string.Format("MCP error: {0}", errorMsg), "ERROR");
                    return new ApiResponse<object> { Error = new ApiError(errorMsg) };
                }

                _logger.Log("MCP request completed successfully", "DEBUG");
                return new ApiResponse<object> { Result = response.Result };
            }
        }

        private string ReadLineWithTimeout(int timeoutMs)
        {
            if (_aborted) return null;

            string result = null;
            Exception readEx = null;
            var thread = new Thread(() =>
            {
                try { result = _stdout.ReadLine(); }
                catch (Exception ex) { readEx = ex; }
            });
            thread.IsBackground = true;
            thread.Start();

            if (thread.Join(timeoutMs))
            {
                if (readEx != null)
                    _logger.Log("ReadLineWithTimeout read error: " + readEx.Message, "WARN");
                return result;
            }
            return null;
        }

        private void SendNotification(string method, object parameters)
        {
            var notification = new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },
                { "method", method },
                { "params", parameters }
            };

            string json = JsonHelper.Serialize(notification);
            _stdin.WriteLine(json);
            _stdin.Flush();
        }
    }
}
