using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WebServerTCP
{
    internal class WebServer(int port)
    {
        private readonly int _port = port;
        private readonly string _webRoot = "C:\\Users\\temok\\Desktop\\WebServerTCP\\WebServerTCP\\webroot";
        private readonly string _logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "webserver.log");
        private TcpListener _listener;
        private bool _isRunning;

        public void Start()
        {
            try
            {
                Log("Server starting...");

                _listener = new TcpListener(System.Net.IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                Log($"Server started on port {_port}");
                Log($"Serving files from: {_webRoot}");
                Log($"Logging to: {_logFilePath}");

                while (_isRunning)
                {
                    try
                    {
                        var client = _listener.AcceptTcpClient();
                        Log($"Client connected: {client.Client.RemoteEndPoint}");
                        ThreadPool.QueueUserWorkItem(HandleClient, client);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error accepting client: {ex.Message}", "ERROR");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Fatal error during server startup: {ex}", "FATAL");
                throw;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            Log("Server stopped");
        }

        private void HandleClient(Object obj)
        {
            using (var client = (TcpClient)obj)
            using (var stream = client.GetStream())
            {
                string clientInfo = client.Client.RemoteEndPoint?.ToString() ?? "unknown client";

                try
                {
                    var request = ReadRequest(stream);
                    Log($"Request from {clientInfo}: {request.Trim()}");

                    if (string.IsNullOrEmpty(request))
                    {
                        Log($"Empty request from {clientInfo}", "WARNING");
                        SendErrorResponse(stream, 400, "Bad Request");
                        return;
                    }

                    var requestParts = request.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (requestParts.Length < 2 || requestParts[0].ToUpper() != "GET")
                    {
                        Log($"Invalid method, WARNING");
                        SendErrorResponse(stream, 405, "Method Not Allowed");
                        return;
                    }

                    var requestedPath = requestParts[1];
                    if (requestedPath.Contains(".."))
                    {
                        Log($"Potential directory traversal attempt from {clientInfo}: {requestedPath}", "SECURITY");
                        SendErrorResponse(stream, 403, "Forbidden - Directory traversal not allowed");
                        return;
                    }

                    if (requestedPath == "/")
                    {
                        requestedPath = "index.html";
                    }

                    var filePath = Path.Combine(_webRoot, requestedPath.TrimStart('/'));
                    Log($"Serving file for {clientInfo}: {filePath}");
                    ServeFile(stream, filePath);
                }
                catch (Exception ex)
                {
                    Log($"Error handling client {clientInfo}: {ex.Message}", "ERROR");
                    SendErrorResponse(stream, 500, "Internal Server Error");
                }
                finally
                {
                    Log($"Client disconnected: {clientInfo}");
                }
            }
        }

        private void Log(string message, string level = "INFO")
        {
            try
            {
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                Console.Write(logEntry);

                var logDir = Path.GetDirectoryName(_logFilePath);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private string ReadRequest(NetworkStream stream)
        {
            var buffer = new byte[1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.ASCII.GetString(buffer, 0, bytesRead);
        }

        private void SendErrorResponse(NetworkStream stream, int statusCode, string statusMessage)
        {
            var errorPage = $"<html><head><title>{statusCode} {statusMessage}</title></head>" +
                            $"<body><h1>Error {statusCode}: {statusMessage}</h1></body></html>";

            var content = Encoding.UTF8.GetBytes(errorPage);

            var header = $"HTTP/1.1 {statusCode} {statusMessage}\r\n" +
                         $"Content-Type: text/html\r\n" +
                         $"Content-Length: {content.Length}\r\n" +
                         $"Connection: close\r\n" +
                         $"\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(content, 0, content.Length);

            Log($"Sent error response: {statusCode} {statusMessage}", "WARNING");
        }

        private void ServeFile(NetworkStream stream, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Log($"File not found: {filePath}", "WARNING");
                SendErrorResponse(stream, 404, "Not Found");
                return;
            }

            var extension = Path.GetExtension(filePath).ToLower();
            if (!IsAllowedExtension(extension))
            {
                Log($"Attempt to access unsupported file type: {extension}", "WARNING");
                SendErrorResponse(stream, 403, "Forbidden - Unsupported file type");
                return;
            }

            try
            {
                var content = File.ReadAllBytes(filePath);
                var mimeType = GetMimeType(extension);

                var header = $"HTTP/1.1 200 OK\r\n" +
                             $"Content-Type: {mimeType}\r\n" +
                             $"Content-Length: {content.Length}\r\n" +
                             $"Connection: close\r\n" +
                             $"\r\n";

                var headerBytes = Encoding.ASCII.GetBytes(header);
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(content, 0, content.Length);

                Log($"Successfully served file: {filePath}");
            }
            catch (Exception ex)
            {
                Log($"Error serving file {filePath}: {ex.Message}", "ERROR");
                SendErrorResponse(stream, 500, "Internal Server Error");
            }
        }

        private bool IsAllowedExtension(string extension)
        {
            return extension switch
            {
                ".html" => true,
                ".htm" => true,
                ".css" => true,
                ".js" => true,
                _ => false
            };
        }

        private string GetMimeType(string extension)
        {
            return extension switch
            {
                ".html" => "text/html",
                ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                _ => "application/octet-stream"
            };
        }

        public static FileExistenceStatus CheckFileExistence(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    return FileExistenceStatus.IsDirectory;
                }
                return FileExistenceStatus.Exists;
            }
            catch (UnauthorizedAccessException)
            {
                return FileExistenceStatus.ExistsNoPermission;
            }
            catch (FileNotFoundException)
            {
                return FileExistenceStatus.DoesNotExist;
            }
            catch (DirectoryNotFoundException)
            {
                return FileExistenceStatus.ParentDirectoryDoesNotExist;
            }
            catch (Exception)
            {
                return FileExistenceStatus.UnknownError;
            }
        }

        public enum FileExistenceStatus
        {
            Exists,
            ExistsNoPermission,
            DoesNotExist,
            IsDirectory,
            ParentDirectoryDoesNotExist,
            UnknownError
        }
    }
}