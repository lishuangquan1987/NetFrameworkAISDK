using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// MCP（Model Control Protocol）客户端，通过子进程标准输入输出与 MCP 服务器通信。
    /// 支持工具发现（tools/list）、工具调用（tools/call）及初始化和关闭握手。
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
        private volatile bool _readCancelled;
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
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        public McpClient(int timeoutMilliseconds)
            : this(timeoutMilliseconds, null)
        {
        }

        /// <summary>
        /// 创建 MCP 客户端并指定超时时间和日志记录器
        /// </summary>
        /// <param name="timeoutMilliseconds">请求超时毫秒数</param>
        /// <param name="logger">日志记录器（可选）</param>
        public McpClient(int timeoutMilliseconds, ILogger logger)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
            _sendLock = new object();
            _logger = logger ?? new ConsoleLogger();
        }

        /// <summary>
        /// 是否已连接到 MCP 服务器进程
        /// </summary>
        public bool IsConnected
        {
            get { return _process != null && !_process.HasExited; }
        }

        /// <summary>
        /// 是否已完成初始化握手
        /// </summary>
        public bool IsInitialized
        {
            get { return _initialized; }
        }

        /// <summary>
        /// 启动 MCP 服务器子进程并建立通信管道
        /// </summary>
        /// <param name="serverPath">MCP 服务器可执行文件路径</param>
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

                _stdin = new StreamWriter(_process.StandardInput.BaseStream, Encoding.UTF8);
                _stdout = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);
                _requestId = 0;

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
        /// 执行 MCP 初始化握手
        /// </summary>
        /// <returns>初始化结果</returns>
        public ApiResponse<bool> Initialize()
        {
            if (!IsConnected)
            {
                return new ApiResponse<bool> { Error = new ApiError("MCP server not connected") };
            }

            try
            {
                var response = SendRequest("initialize", new Dictionary<string, object>
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

                if (!response.IsSuccess)
                {
                    return new ApiResponse<bool> { Error = response.Error };
                }

                _initialized = true;
                return new ApiResponse<bool> { Result = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Error = new ApiError("MCP initialize failed: " + ex.Message) };
            }
        }

        /// <summary>
        /// 获取 MCP 服务器提供的工具列表
        /// </summary>
        /// <returns>工具信息列表</returns>
        public ApiResponse<List<McpToolInfo>> ListTools()
        {
            if (!_initialized)
            {
                return new ApiResponse<List<McpToolInfo>> { Error = new ApiError("MCP client not initialized") };
            }

            try
            {
                var response = SendRequest("tools/list", new Dictionary<string, object>());
                if (!response.IsSuccess)
                {
                    return new ApiResponse<List<McpToolInfo>> { Error = response.Error };
                }

                var result = response.Result;
                if (result == null)
                {
                    return new ApiResponse<List<McpToolInfo>>
                    {
                        Error = new ApiError("MCP list_tools returned null")
                    };
                }

                var content = JsonHelper.Serialize(result);
                var toolsResult = JsonHelper.Deserialize<McpListToolsResult>(content);
                var tools = new List<McpToolInfo>();

                if (toolsResult != null && toolsResult.Tools != null)
                {
                    foreach (var tool in toolsResult.Tools)
                    {
                        tools.Add(new McpToolInfo
                        {
                            Name = tool.Name,
                            Description = tool.Description,
                            InputSchema = tool.InputSchema
                        });
                    }
                }

                return new ApiResponse<List<McpToolInfo>> { Result = tools };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<McpToolInfo>>
                {
                    Error = new ApiError("MCP list_tools failed: " + ex.Message)
                };
            }
        }

        /// <summary>
        /// 调用 MCP 服务器工具
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="arguments">参数对象</param>
        /// <returns>工具执行结果（JSON 字符串）</returns>
        public ApiResponse<string> CallTool(string toolName, object arguments)
        {
            if (!_initialized)
            {
                return new ApiResponse<string> { Error = new ApiError("MCP client not initialized") };
            }

            try
            {
                var response = SendRequest("tools/call", new Dictionary<string, object>
                {
                    { "name", toolName },
                    { "arguments", arguments ?? new Dictionary<string, object>() }
                });

                if (!response.IsSuccess)
                {
                    return new ApiResponse<string> { Error = response.Error };
                }

                var result = response.Result;
                if (result == null)
                {
                    return new ApiResponse<string> { Result = "" };
                }

                return new ApiResponse<string> { Result = JsonHelper.Serialize(result) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<string> { Error = new ApiError("MCP call_tool failed: " + ex.Message) };
            }
        }

        /// <summary>
        /// 发送 MCP 关闭请求
        /// </summary>
        public void Shutdown()
        {
            if (_initialized && IsConnected)
            {
                try
                {
                    SendRequest("shutdown", new Dictionary<string, object>());
                }
                catch
                {
                }
            }
            _initialized = false;
        }

        /// <summary>
        /// 释放所有资源（关闭管道和进程）
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Shutdown();
                _aborted = true;
                _readCancelled = true;
                if (_stdin != null) { try { _stdin.Close(); } catch { } }
                if (_stdout != null) { try { _stdout.Close(); } catch { } }
                if (_process != null)
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            _process.Kill();
                        }
                    }
                    catch { }
                    try { _process.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// 重置客户端状态，允许在超时后重新使用
        /// </summary>
        public void Reset()
        {
            _aborted = false;
            _readCancelled = false;
        }

        /// <summary>
        /// 从标准输出读取一行，带超时保护。
        /// 使用独立线程 + 取消标志，避免线程泄漏问题。
        /// </summary>
        /// <param name="timeoutMs">超时毫秒数</param>
        /// <returns>读取的行，超时返回 null</returns>
        private string ReadLineWithTimeout(int timeoutMs)
        {
            if (_aborted)
            {
                return null;
            }

            string result = null;
            var thread = new Thread(() =>
            {
                try
                {
                    while (!_readCancelled)
                    {
                        if (_stdout.Peek() >= 0)
                        {
                            result = _stdout.ReadLine();
                            break;
                        }
                        Thread.Sleep(50);
                    }
                }
                catch (Exception)
                {
                    result = null;
                }
            });
            thread.IsBackground = true;
            thread.Start();

            if (thread.Join(timeoutMs))
            {
                return result;
            }

            _readCancelled = true;
            Thread.Sleep(100);
            return null;
        }

        /// <summary>
        /// 发送 JSON-RPC 请求到 MCP 服务器（线程安全）
        /// </summary>
        /// <param name="method">JSON-RPC 方法名</param>
        /// <param name="parameters">方法参数</param>
        /// <returns>解析后的响应对象</returns>
        private ApiResponse<object> SendRequest(string method, object parameters)
        {
            lock (_sendLock)
            {
                // 每次请求前重置状态
                Reset();

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
                    return new ApiResponse<object>
                    {
                        Error = new ApiError(errorMsg)
                    };
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
                    return new ApiResponse<object>
                    {
                        Error = new ApiError(errorMsg)
                    };
                }

                _logger.Log("MCP request completed successfully", "DEBUG");
                return new ApiResponse<object> { Result = response.Result };
            }
        }
    }
}