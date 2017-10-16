using System.IO;
using SimpleHttpServer.Contract.Controller;

namespace SimpleHttpServer.Implementation.Controller
{
    public abstract class Controller : IController
    {
        private readonly string _rootDirectory = Directory.GetCurrentDirectory();
        private void CreateTextFile(string controllerName, string actionName)
        {
            var folderPath = Path.Combine(_rootDirectory, controllerName);
            var filePath = Path.Combine(_rootDirectory, controllerName, actionName) + ".txt";
            if (!File.Exists(filePath))
            {
                File.CreateText(filePath);
            }
        }
    }
}
