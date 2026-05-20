using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NetFrameworkAISDK.Common
{
    public class McpToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object InputSchema { get; set; }
    }

    public class McpClient : IDisposable
    {
        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private int _requestId;
        private bool _initialized;
        private bool _disposed;
        private int _timeoutMilliseconds;

        public McpClient()
            : this(30000)
        {
        }

        public McpClient(int timeoutMilliseconds)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        public bool IsConnected
        {
            get { return _process != null && !_process.HasExited; }
        }

        public bool IsInitialized
        {
            get { return _initialized; }
        }

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
                _stdin = new StreamWriter(_process.StandardInput.BaseStream, Encoding.UTF8);
                _stdout = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);
                _requestId = 0;

                return new ApiResponse<bool> { Result = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Error = new ApiError("Failed to connect MCP server: " + ex.Message) };
            }
        }

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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Shutdown();
                if (_stdin != null) { _stdin.Close(); }
                if (_stdout != null) { _stdout.Close(); }
                if (_process != null)
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                    _process.Dispose();
                }
            }
        }

        private ApiResponse<object> SendRequest(string method, object parameters)
        {
            _requestId++;
            var request = new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },
                { "id", _requestId },
                { "method", method },
                { "params", parameters }
            };

            string requestJson = JsonHelper.Serialize(request);

            lock (_stdin)
            {
                _stdin.WriteLine(requestJson);
                _stdin.Flush();
            }

            string responseLine = _stdout.ReadLine();
            if (responseLine == null)
            {
                return new ApiResponse<object> { Error = new ApiError("MCP server returned null response") };
            }

            var response = JsonHelper.Deserialize<McpJsonRpcResponse>(responseLine);
            if (response == null)
            {
                return new ApiResponse<object> { Error = new ApiError("Failed to parse MCP response") };
            }

            if (response.Error != null)
            {
                return new ApiResponse<object>
                {
                    Error = new ApiError(response.Error.Message ?? "MCP request error")
                };
            }

            return new ApiResponse<object> { Result = response.Result };
        }
    }

    internal class McpJsonRpcResponse
    {
        public string Jsonrpc { get; set; }
        public int? Id { get; set; }
        public object Result { get; set; }
        public McpJsonRpcError Error { get; set; }
    }

    internal class McpJsonRpcError
    {
        public int? Code { get; set; }
        public string Message { get; set; }
    }

    internal class McpListToolsResult
    {
        public List<McpToolDefinition> Tools { get; set; }
    }

    internal class McpToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object InputSchema { get; set; }
    }
}