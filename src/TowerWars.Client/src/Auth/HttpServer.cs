using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TowerWars.Client.Auth;

public partial class HttpServer : Node
{
    private HttpListener _listener = null!;
    private bool _isRunning;

    [Signal]
    public delegate void ListenFinishedEventHandler(Godot.Collections.Dictionary<string, string> parameters);

    public void Listen(int port)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");

        try
        {
            _listener.Start();
            GD.Print($"HTTP server listening on port {port}");

            Task.Run(async () => await ListenForCallbackAsync());
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to start HTTP server: {ex.Message}");
            _isRunning = false;
        }
    }

    private async Task ListenForCallbackAsync()
    {
        try
        {
            while (_isRunning && _listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                GD.Print($"Received callback: {request.Url}");

                // Parse query parameters
                var queryParams = new Dictionary<string, string>();
                foreach (string key in request.QueryString.Keys)
                {
                    queryParams[key] = request.QueryString[key];
                }

                // Send success response to browser
                var responseString = @"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>TowerWars - Authentication</title>
                        <style>
                            body {
                                font-family: Arial, sans-serif;
                                display: flex;
                                justify-content: center;
                                align-items: center;
                                height: 100vh;
                                margin: 0;
                                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            }
                            .container {
                                text-align: center;
                                background: white;
                                padding: 40px;
                                border-radius: 10px;
                                box-shadow: 0 10px 40px rgba(0,0,0,0.2);
                            }
                            h1 { color: #667eea; margin: 0 0 20px 0; }
                            p { color: #666; font-size: 18px; }
                            .success { color: #4caf50; font-size: 48px; margin-bottom: 20px; }
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='success'>✓</div>
                            <h1>Authentication Successful!</h1>
                            <p>You can close this window and return to TowerWars.</p>
                        </div>
                        <script>
                            setTimeout(() => window.close(), 2000);
                        </script>
                    </body>
                    </html>
                ";

                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;

                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();

                // Convert to Godot Dictionary and emit signal
                var godotDict = new Godot.Collections.Dictionary<string, string>();
                foreach (var kvp in queryParams)
                {
                    godotDict[kvp.Key] = kvp.Value;
                }

                CallDeferred(MethodName.EmitSignal, SignalName.ListenFinished, godotDict);

                // Stop listening after receiving callback
                Stop();
                break;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"HTTP server error: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (_listener != null && _listener.IsListening)
        {
            _listener.Stop();
            _listener.Close();
            GD.Print("HTTP server stopped");
        }
        _isRunning = false;
    }

    public override void _ExitTree()
    {
        Stop();
    }
}
