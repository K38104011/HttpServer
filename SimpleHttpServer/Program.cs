using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SimpleHttpServer
{
    class Program
    {
        private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>
        {
            { ".css", "text/css" },
            { ".html", "text/html" }
        };

        static void Main(string[] args)
        {
            HttpListener httpListener = new HttpListener();
            var port = "2211";
            var rootDirectory = Directory.GetCurrentDirectory();
            httpListener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            httpListener.Prefixes.Add("http://localhost:" + port + "/");
            httpListener.Start();
            Console.WriteLine("Listening...");
            const int maxRequest = 5;
            var isCreate = true;
            while (true)
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
                }
                response.ContentLength64 = responseBytes.Length;
                response.ContentType = GetMimeType(fileName);
                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            });
        }

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return MimeTypes[extension];
        }
    }
}
