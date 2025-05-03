using System;
using System.Collections.Generic;
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
        private TcpListener _listener;
        private bool _isRunning;

        public void Start()
        {
            _listener = new TcpListener(System.Net.IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"Server started on port {_port}");
            Console.WriteLine($"Serving files from: {_webRoot}");

            while (_isRunning) {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        private void HandleClient(Object obj)
        {
            using (var client = (TcpClient)obj)
            using (var stream = client.GetStream())
            {
                try
                {
                    var request = ReadRequest(stream);
                    Console.WriteLine($"Request: {request}");

                    if (string.IsNullOrEmpty(request))
                    {
                        SendErrorResponse(stream, 400, "Bad Request");
                        return;
                    }

                    var requestParts = request.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (requestParts.Length < 2 || requestParts[0].ToUpper() != "GET")
                    {
                        SendErrorResponse(stream, 405, "Method Not Allowed");
                        return;
                    }

                    var requestedPath = requestParts[1];
                    if (requestedPath.Contains(".."))
                    {
                        SendErrorResponse(stream, 403, "Forbidden - Directory traversal not allowed");
                        return;
                    }

                    if (requestedPath == "/")
                    {
                        requestedPath = "index.html";
                    }

                    var filePath = Path.Combine(_webRoot, requestedPath.TrimStart('/'));
                    ServeFile(stream, filePath);
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error handling client: {ex.Message}");
                    SendErrorResponse(stream, 500, "Internal Server Error");
                }
            }
        }


        private string ReadRequest(NetworkStream stream)
        {
            var buffer = new byte[1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.ASCII.GetString(buffer,0, bytesRead);
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
        }

        private void ServeFile(NetworkStream stream, string filePath)
        {
            if (!File.Exists(filePath))
            {
                SendErrorResponse(stream, 404, "Not Found");
                return;
            }

            var extension = Path.GetExtension(filePath).ToLower();
            if (!IsAllowedExtension(extension)) {
                SendErrorResponse(stream, 403, "Forbidden - Unsupported file type");
                return;
            }

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
