# WebServerTCP - Lightweight HTTP Server

## Features
- **HTTP/1.1** GET request handling
- **Static file serving** (HTML/CSS/JS)
- **Multi-client support** via ThreadPool
- **Security protections** against path traversal
- **Detailed logging** (console + file)

## Usage
```csharp
var server = new WebServer(8080);  // Specify port
server.Start();                    // Start server
server.Stop();                     // Graceful shutdown
```
