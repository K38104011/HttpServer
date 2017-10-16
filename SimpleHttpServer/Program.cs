using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading.Tasks;
using RazorEngine;
using RazorEngine.Templating;
using SimpleHttpServer.Implementation.Controller;
using Encoding = System.Text.Encoding;

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
        //Todo write cache, upload file
        static int Main(string[] args)
        {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                // RazorEngine cannot clean up from the default appdomain...
                Console.WriteLine("Switching to secound AppDomain, for RazorEngine...");
                AppDomainSetup adSetup = new AppDomainSetup
                {
                    ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
                };
                var current = AppDomain.CurrentDomain;
                // You only need to add strongnames when your appdomain is not a full trust environment.
                var strongNames = new StrongName[0];

                var domain = AppDomain.CreateDomain(
                    "MyMainDomain", null,
                    current.SetupInformation, new PermissionSet(PermissionState.Unrestricted),
                    strongNames);
                var exitCode = domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location);
                // RazorEngine will cleanup. 
                AppDomain.Unload(domain);
                return exitCode;
            }
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
            return 0;
        }

        private static Task CreateProcess(HttpListener httpListener, string rootDirectory)
        {
            return Task.Run(async () =>
            {
                var httpListenerContext = await httpListener.GetContextAsync();
                var localPath = httpListenerContext.Request.Url.LocalPath;
                var fileName = localPath.Substring(1);
                var request = httpListenerContext.Request;
                byte[] responseBytes;
                var methodHttp = httpListenerContext.Request.HttpMethod;
                if (methodHttp == System.Net.WebRequestMethods.Http.Get)
                {
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = "index.html";
                    }
                    var pathFileName = Path.Combine(rootDirectory, fileName);
                    var response = httpListenerContext.Response;
                    response.ContentType = GetMimeType(fileName);
                    if (!File.Exists(pathFileName) && string.IsNullOrWhiteSpace(response.ContentType))
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        responseBytes = Encoding.UTF8.GetBytes("Not found");

                        var tokenString = request.RawUrl;
                        var tokens = tokenString.Split('/');

                        if (tokens.Length >= 2 && (Path.GetExtension(pathFileName) == ".cshtml" ||
                            string.IsNullOrWhiteSpace(Path.GetExtension(pathFileName)))
                                && Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Views")))
                        {
                            var controller = tokens[0];
                            var action = tokens[1];

                            //if (!string.IsNullOrWhiteSpace(tokens[1]) && !string.IsNullOrWhiteSpace(tokens[2]))
                            //{
                            //    var concreteController = new ConcreteController();
                            //    var createTextMethod = typeof(Controller).GetMethod("CreateTextFile", BindingFlags.NonPublic | BindingFlags.Instance);
                            //    createTextMethod.Invoke(concreteController, new object[] {tokens[1], tokens[2]});
                            //}

                            //var folderViews = Path.Combine(Directory.GetCurrentDirectory(), "Views");
                            //var filesInViewsFoler = new DirectoryInfo(folderViews).GetFiles();
                            //var cshtmlFile = string.Empty;
                            //foreach (var file in filesInViewsFoler)
                            //{
                            //    if (File.Exists(file.FullName))
                            //    {
                            //        cshtmlFile = file.FullName;
                            //        break;
                            //    }
                            //}
                            //if (string.IsNullOrWhiteSpace(cshtmlFile))
                            //{
                            //    var folderControl = Path.Combine(folderViews, controller);
                            //    var controlSubDirectories = new DirectoryInfo(folderControl).GetDirectories();
                            //    foreach (var controlSubDirectory in controlSubDirectories)
                            //    {
                            //        var cshtmlFilePath = Path.Combine(controlSubDirectory.FullName,
                            //            Path.GetFileNameWithoutExtension(action) + ".cshtml");
                            //        if (File.Exists(cshtmlFilePath))
                            //        {
                            //            cshtmlFile = cshtmlFilePath;
                            //            break;
                            //        }
                            //    }
                            //}
                            var cshtmlFile = Path.Combine(rootDirectory, "Views\\DB\\Init.cshtml");
                            if (!string.IsNullOrWhiteSpace(cshtmlFile))
                            {
                                response.StatusCode = (int)HttpStatusCode.OK;
                                var template = File.ReadAllText(cshtmlFile);
                                var result = Engine.Razor.RunCompile(template, Guid.NewGuid().ToString(), null,
                                    new { Name = "Giang" });
                                responseBytes = Encoding.UTF8.GetBytes(result);
                            }
                        }

                    }
                    else
                    {
                        responseBytes = File.ReadAllBytes(pathFileName);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.ContentType = GetMimeType(fileName);
                    }
                    response.ContentLength64 = responseBytes.Length;
                    var acceptEncodings = httpListenerContext.Request.Headers["Accept-Encoding"];
                    if (acceptEncodings.Contains("gzip") && !".png,.jpg,.map".Split(',').Contains(Path.GetExtension(fileName)))
                    {
                        try
                        {
                            response.Headers.Add("Content-Encoding", "gzip");
                            using (var inputStream = new MemoryStream(responseBytes))
                            {
                                using (var stream = response.OutputStream)
                                {
                                    using (var outputStream = new MemoryStream())
                                    {
                                        using (var compressStream = new GZipStream(outputStream, CompressionMode.Compress, true))
                                        {
                                            await inputStream.CopyToAsync(compressStream);
                                        }
                                        var temp = outputStream.ToArray();
                                        response.ContentLength64 = temp.Length;
                                        await stream.WriteAsync(temp, 0, temp.Length);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    }
                    else
                    {
                        using (var stream = response.OutputStream)
                        {
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        }
                    }
                }
                else if (methodHttp == WebRequestMethods.Http.Post)
                {
                    if (!request.HasEntityBody)
                    {
                        return;
                    }
                    var body = await new StreamReader(request.InputStream).ReadToEndAsync();
                    var fields = body.Split('&');
                    if (!fields.Any())
                    {
                        return;
                    }
                    var tokens = request.RawUrl.Split('\\');
                    var controllerName = tokens[1];
                    var actionName = tokens[2];
                    if (!tokens.Any() || tokens.Length != 3)
                    {
                        return;
                    }
                    InitializeShemaDatabase(controllerName, actionName);
                }
            });
        }

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return MimeTypes.ContainsKey(extension) ? MimeTypes[extension] : string.Empty;
        }

        private static void InitializeShemaDatabase(string controllerName, string actionName)
        {
            if (controllerName == "DB" && actionName == "Init")
            {
                var dbFolder = Path.Combine(Directory.GetCurrentDirectory(), controllerName);
                if (!Directory.Exists(dbFolder))
                {
                    new DirectoryInfo(dbFolder).Create();
                }
                
            }
        }

    }
}
