using System.Text.Json;
using MemoryMcp.Server;

MemoryMcpServer? server = null;

try
{
    Console.Error.WriteLine("[MemoryMCP] Initializing server...");
    server = new MemoryMcpServer();
    await server.InitializeAsync();
    Console.Error.WriteLine("[MemoryMCP] Server initialized");

    var handler = new McpJsonRpcHandler();
    var tools = server.GetTools();
    Console.Error.WriteLine($"[MemoryMCP] Loaded {tools.Count} tools");

    // Main loop: read JSON-RPC requests from stdin
    string? line;
    while ((line = Console.ReadLine()) != null)
    {
        try
        {
            Console.Error.WriteLine($"[MemoryMCP] Received: {line}");
            var request = handler.ParseRequest(line);
            Console.Error.WriteLine($"[MemoryMCP] Method: {request.Method}");

            // Handle notifications (no response needed)
            if (request.Method?.StartsWith("notifications/") == true)
            {
                Console.Error.WriteLine($"[MemoryMCP] Notification received, no response needed");
                continue;
            }

            McpJsonRpcHandler.JsonRpcResponse<object> response = request.Method switch
            {
                "initialize" => new()
                {
                    Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "MemoryMCP",
                            version = "1.0.0"
                        }
                    }
                },
                "tools/list" => new()
                {
                    Result = new { tools }
                },
                "tools/call" => await HandleToolCall(request),
                _ => new()
                {
                    Error = new()
                    {
                        Code = -32601,
                        Message = "Method not found"
                    }
                }
            };

            response.Id = request.Id;
            var responseJson = handler.SerializeResponse(response);
            Console.Error.WriteLine($"[MemoryMCP] Sending: {responseJson}");
            Console.WriteLine(responseJson);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MemoryMCP] Error processing request: {ex.Message}");
            Console.Error.WriteLine($"[MemoryMCP] Stack: {ex.StackTrace}");
            var errResponse = new McpJsonRpcHandler.JsonRpcResponse<object>
            {
                Error = new()
                {
                    Code = -32700,
                    Message = "Parse error"
                }
            };
            Console.WriteLine(handler.SerializeResponse(errResponse));
        }
    }
    
    Console.Error.WriteLine("[MemoryMCP] Stdin closed, exiting");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

async Task<McpJsonRpcHandler.JsonRpcResponse<object>> HandleToolCall(McpJsonRpcHandler.JsonRpcRequest request)
{
    if (server == null)
        return new()
        {
            Error = new() { Code = -32603, Message = "Server not initialized" }
        };

    if (!request.Params.HasValue)
        return new()
        {
            Error = new() { Code = -32602, Message = "Invalid params" }
        };

    var p = request.Params.Value;
    if (!p.TryGetProperty("name", out var nameElem))
        return new()
        {
            Error = new() { Code = -32602, Message = "Missing tool name" }
        };

    var toolName = nameElem.GetString() ?? "";
    JsonElement? arguments = null;
    if (p.TryGetProperty("arguments", out var argsElem))
    {
        arguments = argsElem;
    }

    try
    {
        var result = await server.HandleToolCall(toolName, arguments);
        return result;
    }
    catch (Exception ex)
    {
        return new()
        {
            Error = new()
            {
                Code = -32603,
                Message = ex.Message
            }
        };
    }
}

