namespace WebServerTCP
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 8080;
            
            var server = new WebServer(port);
            server.Start();

            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.Stop();
        }
    }
}
