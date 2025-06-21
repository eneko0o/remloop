using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;

namespace remloop
{
    public class PluginManager
    {
        private readonly List<(IConsolePlugin Plugin, Assembly Assembly)> _plugins = new List<(IConsolePlugin, Assembly)>();
        private readonly IConsoleApi _consoleApi;
        private readonly string _pluginsPath;

        public PluginManager(IConsoleApi consoleApi)
        {
            _consoleApi = consoleApi;
            _pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            try
            {
                Directory.CreateDirectory(_pluginsPath);
                _consoleApi.Log($"Plugin directory: {_pluginsPath}", Color.Cyan);
            }
            catch (Exception ex)
            {
                _consoleApi.Log($"Error creating plugin directory: {ex.Message}", Color.Red);
            }
        }

        public void LoadPlugins()
        {
            _plugins.Clear();
            if (!Directory.Exists(_pluginsPath))
            {
                _consoleApi.Log($"Plugin directory {_pluginsPath} does not exist.", Color.Red);
                return;
            }

            var pluginFiles = Directory.GetFiles(_pluginsPath, "*.cs");
            if (pluginFiles.Length == 0)
            {
                _consoleApi.Log("No plugins found in Plugins directory.", Color.Yellow);
            }

            foreach (var file in pluginFiles)
            {
                _consoleApi.Log($"Attempting to load plugin from: {Path.GetFileName(file)}", Color.Cyan);
                try
                {
                    string code = File.ReadAllText(file);
                    var assembly = CompilePlugin(code, file);
                    if (assembly != null)
                    {
                        var pluginType = assembly.GetTypes()
                            .FirstOrDefault(t => typeof(IConsolePlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        if (pluginType != null)
                        {
                            var plugin = (IConsolePlugin)Activator.CreateInstance(pluginType);
                            if (string.IsNullOrEmpty(plugin.Version))
                            {
                                _consoleApi.Log($"Error: Plugin {plugin.Name} does not specify a version.", Color.Red);
                                continue;
                            }
                            if (string.IsNullOrEmpty(plugin.Name))
                            {
                                _consoleApi.Log($"Error: Plugin in {Path.GetFileName(file)} has empty name.", Color.Red);
                                continue;
                            }
                            if (plugin.Name.Length > 32)
                            {
                                _consoleApi.Log($"Error: Plugin name '{plugin.Name}' exceeds 32 characters.", Color.Red);
                                continue;
                            }
                            if (plugin.Name.Contains(" "))
                            {
                                _consoleApi.Log($"Error: Plugin name '{plugin.Name}' contains spaces.", Color.Red);
                                continue;
                            }
                            if (_plugins.Any(p => p.Plugin.Name.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                _consoleApi.Log($"Error: Plugin name '{plugin.Name}' is already used by another plugin.", Color.Red);
                                continue;
                            }

                            var pluginApi = new Form1.ConsoleApi(Form1.Instance, plugin.Name);
                            plugin.Initialize(pluginApi);
                            _plugins.Add((plugin, assembly));
                            _consoleApi.Log($"Loaded plugin: {plugin.Name} v{plugin.Version}", Color.Green);
                        }
                        else
                        {
                            _consoleApi.Log($"No valid plugin type found in {Path.GetFileName(file)}.", Color.Red);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _consoleApi.Log($"Error loading plugin from {Path.GetFileName(file)}: {ex.Message}", Color.Red);
                }
            }
        }

        private Assembly CompilePlugin(string code, string filePath)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);
            var assemblyName = Path.GetRandomFileName();

            var references = new[]
            {
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.IO.File).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Net.Http.HttpClient).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Xml.XmlDocument).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Diagnostics.Process).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Diagnostics.Stopwatch).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.Component).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.PropertyChangedEventArgs).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Security.Cryptography.SHA256).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Form).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Color).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Net.Sockets.Socket).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Net.NetworkInformation.Ping).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Security.Principal.WindowsIdentity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Security.Claims.ClaimsIdentity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Text.RegularExpressions.Regex).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.InteropServices.DllImportAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Timer).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.IO.Compression.ZipFile).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
            };

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (result.Success)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    return Assembly.Load(ms.ToArray());
                }

                var errors = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"{d.Location.SourceSpan}: {d.GetMessage()}"));
                _consoleApi.Log($"Compilation errors in {Path.GetFileName(filePath)}:\n{errors}", Color.Red);
                return null;
            }
        }

        public IEnumerable<(string Name, string Version, bool Enabled, string Description, IEnumerable<string> Commands)> GetPlugins()
        {
            return _plugins.Select(p => (
                p.Plugin.Name,
                p.Plugin.Version,
                true,
                p.Plugin.Description,
                p.Plugin.GetCommands() ?? Enumerable.Empty<string>()
            ));
        }

        public bool DisablePlugin(string name)
        {
            var plugin = _plugins.FirstOrDefault(p => p.Plugin.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (plugin.Plugin != null)
            {
                _plugins.Remove(plugin);
                _consoleApi.Log($"Disabled plugin: {name}", Color.Yellow);
                return true;
            }
            _consoleApi.Log($"No plugin {name} found.", Color.Red);
            return false;
        }

        public bool EnablePlugin(string name)
        {
            var pluginFile = Directory.GetFiles(_pluginsPath, "*.cs")
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));

            if (pluginFile != null)
            {
                try
                {
                    string code = File.ReadAllText(pluginFile);
                    var assembly = CompilePlugin(code, pluginFile);
                    if (assembly != null)
                    {
                        var pluginType = assembly.GetTypes()
                            .FirstOrDefault(t => typeof(IConsolePlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        if (pluginType != null)
                        {
                            var plugin = (IConsolePlugin)Activator.CreateInstance(pluginType);
                            if (string.IsNullOrEmpty(plugin.Version))
                            {
                                _consoleApi.Log($"Error: Plugin {plugin.Name} does not specify a version.", Color.Red);
                                return false;
                            }
                            if (string.IsNullOrEmpty(plugin.Name))
                            {
                                _consoleApi.Log($"Error: Plugin in {Path.GetFileName(pluginFile)} has empty name.", Color.Red);
                                return false;
                            }
                            if (plugin.Name.Length > 32)
                            {
                                _consoleApi.Log($"Error: Plugin name '{plugin.Name}' exceeds 32 characters.", Color.Red);
                                return false;
                            }
                            if (plugin.Name.Contains(" "))
                            {
                                _consoleApi.Log($"Error: Plugin name '{plugin.Name}' contains spaces.", Color.Red);
                                return false;
                            }
                            if (_plugins.Any(p => p.Plugin.Name.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                _consoleApi.Log($"Error: Plugin name '{plugin.Name}' is already used by another plugin.", Color.Red);
                                return false;
                            }

                            var pluginApi = new Form1.ConsoleApi(Form1.Instance, plugin.Name);
                            plugin.Initialize(pluginApi);
                            _plugins.Add((plugin, assembly));
                            _consoleApi.Log($"Enabled plugin: {plugin.Name} v{plugin.Version}", Color.Green);
                            return true;
                        }
                        _consoleApi.Log($"No valid plugin type found in {Path.GetFileName(pluginFile)}.", Color.Red);
                    }
                }
                catch (Exception ex)
                {
                    _consoleApi.Log($"Error enabling plugin {name}: {ex.Message}", Color.Red);
                }
            }
            else
            {
                _consoleApi.Log($"Plugin file for {name} not found in Plugins directory.", Color.Red);
            }
            return false;
        }
    }
}