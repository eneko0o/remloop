using System.Collections.Generic;
using System.Drawing;

namespace remloop
{
    public interface IConsolePlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get => null; }
        void Initialize(IConsoleApi consoleApi);
        IEnumerable<string> GetCommands();
    }

    public interface IConsoleApi
    {
        void RegisterCommand(string command, Action<string[]> handler, string description = null);
        void Log(string message, Color color);
        void ClearConsole();
    }
}