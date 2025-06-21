# remloop
remloop is a lightweight, extensible console application built with C# and Windows Forms, designed for executing commands with a plugin-based architecture. It features a dark-themed UI, command suggestions, and robust plugin management. This README provides instructions on using the console, creating plugins, and managing them.
Getting Started
Prerequisites

.NET Framework (version compatible with the application, typically .NET Framework 4.8 or later)
Windows operating system (due to Windows Forms dependency)

Installation

Clone or download the repository from GitHub.
Open the solution in Visual Studio.
Build the project to generate the executable.
Run the remloop.exe from the bin/Debug or bin/Release folder.


The application creates a Plugins directory in the executable's directory for loading plugins.

Using the Console

Launch the Application: Run remloop.exe. The console window opens with a dark theme, a command input box at the bottom, and a log area above it.
Enter Commands: Type a command in the input box and press Enter. Commands are case-insensitive.
Example: Type help to list all available commands.


Command Feedback:
Valid commands turn the input text white.
Invalid commands turn the input text red, with suggestions for similar commands.


Core Commands:
clear: Clears the console output.
help: Displays all available commands with descriptions.
info: Shows version and feature details of remloop.
versions: Displays the version history.
plist: Lists all loaded plugins with their status.
pinfo <plugin_name>: Shows detailed information about a specific plugin.
close, quit, q: Closes the application.




Use help to explore commands, including those added by plugins.

Creating Plugins
Plugins extend remloop functionality by adding custom commands. They are written in C# and compiled dynamically from .cs files placed in the Plugins directory.
Plugin Structure
A plugin must implement the IConsolePlugin interface:
```csharp
public interface IConsolePlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get => null; }
    void Initialize(IConsoleApi consoleApi);
    IEnumerable<string> GetCommands();
}
```

Steps to Create a Plugin

> Create a C# File:

> Create a .cs file (e.g., MyPlugin.cs) in the Plugins directory.


Implement the Plugin:

Define a class that implements IConsolePlugin.
Set Name (max 32 characters, no spaces), Version, and optionally Description.
Use Initialize to register commands via IConsoleApi.
Return command names in GetCommands.

**Example:**

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using remloop;
public class MyPlugin : IConsolePlugin
{
    public string Name => "plugin";
    public string Version => "1.0";
    public string Description => "example description";
    private IConsoleApi _api;
    public void Initialize(IConsoleApi consoleApi)
    {
        _api = consoleApi;
        _api.RegisterCommand("hello", HandleHello, "Print hello in console");
    }
    private void HandleHello(string[] args)
    {
        _api.Log("Hello from MyPlugin!", Color.Green);
    }
    public IEnumerable<string> GetCommands()
    {
        return new[] { "hello" };
    }
}
```

> Place in Plugins Directory:

> Save the .cs file in the Plugins folder.


**Load the Plugin:**

Restart remloop or reload plugins (if supported in future updates).
The console logs plugin loading status (green for success, red for errors).



**Plugin Guidelines**

**Naming:** Plugin names must be unique, up to 32 characters, and contain no spaces.
**Commands:** Register commands using IConsoleApi.RegisterCommand. If multiple plugins define the same command, prefix with the plugin name (e.g., MyPlugin:hello).
**Dependencies:** Plugins can reference .NET assemblies listed in PluginManager.cs (e.g., System.Linq, System.Net.Http).
**Error Handling:** Handle exceptions in command handlers to prevent crashes. Errors during plugin loading are logged to the console.


Ensure plugins are tested thoroughly, as compilation errors or invalid configurations (e.g., missing Version) prevent loading.

**Managing Plugins**

> List Plugins: Use plist to view loaded plugins, their versions, and status.

> Plugin Info: Use pinfo <plugin_name> to get details about a specific plugin, including commands.

> Add/Remove Plugins:

> Add: Place a .cs file in the Plugins directory.

> Remove: Delete the .cs file from the Plugins directory and restart remloop.


Enable/Disable: Currently, plugins are enabled by default. Disabling requires removing the file or future API enhancements.
