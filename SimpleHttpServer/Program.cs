using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SimpleHttpServer
{
    class Program
    {
        private static readonly ConcurrentDictionary<string, string> MimeTypes = new ConcurrentDictionary<string, string>(new Dictionary<string, string>
        {
            { ".css", "text/css" },
            { ".html", "text/html" },
            {".js", "application/x-javascript"},
            {".png", "image/png"},
            {".jpg", "image/jpeg"}
        });

        private static bool _isActive = true;

        static void Main(string[] args)
        {
            var httpListener = new HttpListener();
            var port = "2211";
            var rootDirectory = Directory.GetCurrentDirectory();
            if (args.Any())
            {
                if (!string.IsNullOrWhiteSpace(args[0]))
                {
                    port = args[0];
                }
                if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                {
                    rootDirectory = args[1];
                }
            }
            httpListener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
            httpListener.Prefixes.Add(string.Format("http://localhost:{0}/", port));
            httpListener.Start();
            Console.WriteLine("Listening on port {0}", port);
            Console.WriteLine("Root: {0}", rootDirectory);
            const int maxRequest = 5;
            var isCreate = true;
            Task.Run(() =>
            {
                while (!Console.KeyAvailable)
                {
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        _isActive = false;
                        httpListener.Stop();
                    }
                }
            });
            while (_isActive)
            {
                if (isCreate)
                {
                    var tasks = new List<Task>();
                    for (var i = 0; i < maxRequest; i++)
                    {
                        tasks.Add(CreateProcess(httpListener, rootDirectory));
                        isCreate = false;
                    }
                    Task.Run(async () =>
                    {
                        await Task.WhenAll(tasks);
                        isCreate = true;
                    });
                }
            }
        }

        private static Task CreateProcess(HttpListener httpListener, string rootDirectory)
        {
            return Task.Run(async () =>
            {
                var httpListenerContext = await httpListener.GetContextAsync();
                var localPath = httpListenerContext.Request.Url.LocalPath;
                var fileName = localPath.Substring(1);
                byte[] responseBytes;
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = "index.html";
                }
                var pathFileName = Path.Combine(rootDirectory, fileName);
                var response = httpListenerContext.Response;
                if (!File.Exists(pathFileName))
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    responseBytes = Encoding.UTF8.GetBytes("Not found");
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    responseBytes = File.ReadAllBytes(pathFileName);
                    response.ContentType = GetMimeType(fileName);
                }
                response.ContentLength64 = responseBytes.Length;
                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            });
        }

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return MimeTypes.ContainsKey(extension) ? MimeTypes[extension] : "text/plain";
        }
    }
}
