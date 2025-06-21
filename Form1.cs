using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace remloop
{
    public partial class Form1 : Form
    {
        private readonly Color DarkBackground = Color.FromArgb(25, 25, 25);
        private readonly Color DarkControl = Color.FromArgb(40, 40, 40);
        private readonly Color AccentColor = Color.FromArgb(0, 122, 204);
        private readonly PluginManager _pluginManager;
        private readonly Dictionary<string, (Action<string[]> Handler, string Description, string PluginName)> _commands = new Dictionary<string, (Action<string[]>, string, string)>();
        public static Form1 Instance { get; private set; }

        public Form1()
        {
            InitializeComponent();
            Instance = this;
            this.Icon = new Icon("icon.ico");
            InitializeCustomStyles();
            _pluginManager = new PluginManager(new ConsoleApi(this));
            InitializeCommands();
            this.Shown += Form1_Shown;
            commandTextBox.TextChanged += commandTextBox_TextChanged;
        }

        private void commandTextBox_TextChanged(object sender, EventArgs e)
        {
            string input = commandTextBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(input))
            {
                commandTextBox.ForeColor = AccentColor;
                return;
            }
            bool isValidCommand = _commands.Any(c =>
            {
                string baseCommand = c.Key.Contains(":") ? c.Key.Split(':').Last() : c.Key;
                return baseCommand.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                       baseCommand.StartsWith(input, StringComparison.OrdinalIgnoreCase) ||
                       input.StartsWith(baseCommand + " ", StringComparison.OrdinalIgnoreCase);
            });

            commandTextBox.ForeColor = isValidCommand ? Color.White : Color.Red;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    _pluginManager.LoadPlugins();
                    this.Invoke((Action)(() =>
                    {
                        AppendToLog("Plugin loading completed.", Color.Cyan);
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke((Action)(() =>
                    {
                        AppendToLog($"Error during plugin loading: {ex.Message}", Color.Red);
                    }));
                }
            });
        }

        private void InitializeCustomStyles()
        {
            this.BackColor = DarkBackground;
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Text = string.Empty;
            this.ControlBox = false;

            logTextBox.BackColor = DarkControl;
            logTextBox.ForeColor = Color.White;
            logTextBox.BorderStyle = BorderStyle.None;
            logTextBox.Font = new Font("Consolas", 10);
            logTextBox.SelectionColor = AccentColor;

            commandTextBox.BackColor = DarkControl;
            commandTextBox.ForeColor = AccentColor;
            commandTextBox.BorderStyle = BorderStyle.FixedSingle;
            commandTextBox.Font = new Font("Consolas", 10);

            statusStrip.BackColor = DarkControl;
            statusStrip.ForeColor = Color.White;
            statusStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            statusLabel.ForeColor = Color.White;

            btnClear.BackColor = DarkControl;
            btnClear.ForeColor = Color.White;
            btnClear.FlatStyle = FlatStyle.Flat;
            btnClear.FlatAppearance.BorderColor = AccentColor;
        }

        private void InitializeCommands()
        {
            _commands.Add("clear", (args => logTextBox.Clear(), "Clears the console output", "Core"));
            _commands.Add("help", (args => Consolehelp(), "Displays available commands with descriptions", "Core"));
            _commands.Add("info", (args => Consoleinfo(), "Shows remloop version and information", "Core"));
            _commands.Add("versions", (args => ConsoleInfoHistory(), "Shows the version history of remloop", "Core"));
            _commands.Add("plist", (args =>
            {
                AppendToLog("Loaded plugins:", Color.Cyan);
                var plugins = _pluginManager.GetPlugins();
                if (!plugins.Any())
                {
                    AppendToLog("  No plugins loaded.", Color.LightGray);
                }
                else
                {
                    foreach (var plugin in plugins)
                    {
                        AppendToLog($"  {plugin.Name} v{plugin.Version} ({(plugin.Enabled ? "loaded" : "unloaded")})", Color.LightGray);
                    }
                }
            }, "Lists all loaded plugins", "Core"));
            _commands.Add("pinfo", (args => ConsolePluginInfo(args), "Shows information about a specific plugin", "Core"));

            Action<string[]> closeAction = args => this.Invoke(Close);
            _commands.Add("close", (closeAction, "Closes the application", "Core"));
            _commands.Add("quit", (closeAction, "Closes the application", "Core"));
            _commands.Add("q", (closeAction, "Closes the application", "Core"));
        }

        private void ConsolePluginInfo(string[] args)
        {
            if (args.Length == 0)
            {
                AppendToLog("Error: Plugin name not provided. Usage: pinfo <plugin_name>", Color.Red);
                return;
            }

            string pluginName = args[0];
            var plugin = _pluginManager.GetPlugins()
                .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

            if (plugin.Name == null)
            {
                AppendToLog($"Error: Plugin '{pluginName}' not found.", Color.Red);
                return;
            }

            AppendToLog($"Plugin information for {plugin.Name}:", Color.Cyan);
            AppendToLog($"  Name: {plugin.Name}", Color.LightGray);
            AppendToLog($"  Version: {plugin.Version}", Color.LightGray);
            AppendToLog($"  Status: {(plugin.Enabled ? "loaded" : "unloaded")}", Color.LightGray);
            AppendToLog($"  Description: {plugin.Description ?? "<no description>"}", Color.LightGray);

            var commands = plugin.Commands?.ToList() ?? new List<string>();
            AppendToLog($"  Commands: {(commands.Any() ? string.Join(", ", commands) : "<none>")}", Color.LightGray);
        }

        private void commandTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(commandTextBox.Text))
            {
                e.SuppressKeyPress = true;
                ProcessCommand(commandTextBox.Text.Trim());
                commandTextBox.Clear();
                statusLabel.Text = "Ready";
            }
        }

        private void ProcessCommand(string input)
        {
            AppendToLog($"> {input}", Color.White);

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            var commandInput = parts[0].ToLower();
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            string commandKey = commandInput;
            if (commandInput.Contains(":"))
            {
                var split = commandInput.Split(':');
                if (split.Length == 2 && !string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                {
                    commandKey = $"{split[0].ToLower()}:{split[1].ToLower()}";
                }
            }

            if (_commands.TryGetValue(commandKey, out var commandInfo))
            {
                commandInfo.Handler(args);
                return;
            }
            var matchingCommands = _commands
                .Where(c => c.Key.EndsWith($":{commandInput}", StringComparison.OrdinalIgnoreCase) || c.Key.Equals(commandInput, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingCommands.Count == 1)
            {
                matchingCommands[0].Value.Handler(args);
            }
            else if (matchingCommands.Count > 1)
            {
                AppendToLog($"Error: Ambiguous command '{commandInput}'. Several plugins define this command. Use one of the following:", Color.Red);
                foreach (var cmd in matchingCommands.OrderBy(c => c.Value.PluginName))
                {
                    string cmdName = cmd.Value.PluginName == "Core" ? cmd.Key : $"{cmd.Value.PluginName}:{cmd.Key.Split(':').Last()}";
                    AppendToLog($"  {cmdName} - {cmd.Value.Description ?? "<no description>"} [{cmd.Value.PluginName}]", Color.Yellow);
                }
            }
            else
            {
                AppendToLog($"Error: Command '{commandInput}' not recognized.", Color.Red);
                SuggestCommands(commandInput);
            }
        }

        private void SuggestCommands(string input)
        {
            var suggestions = _commands
                .Select(c =>
                {
                    string commandName = c.Key;
                    string baseCommand = c.Key.Contains(":") ? c.Key.Split(':').Last() : c.Key;
                    int distance = LevenshteinDistance(input, baseCommand);
                    int conflictingCommands = _commands
                        .Count(other => (other.Key.EndsWith($":{baseCommand}", StringComparison.OrdinalIgnoreCase) || other.Key.Equals(baseCommand, StringComparison.OrdinalIgnoreCase)));
                    if (conflictingCommands > 1)
                    {
                        commandName = $"{c.Value.PluginName}:{baseCommand}";
                    }
                    else
                    {
                        commandName = baseCommand;
                    }
                    return new
                    {
                        CommandName = commandName,
                        Key = c.Key,
                        Description = c.Value.Description,
                        PluginName = c.Value.PluginName,
                        Distance = distance
                    };
                })
                .OrderBy(c => c.Distance)
                .ThenBy(c => c.CommandName)
                .Take(5)
                .ToList();

            if (suggestions.Any())
            {
                AppendToLog("Did you mean one of these commands?", Color.Yellow);
                foreach (var suggestion in suggestions)
                {
                    AppendToLog($"  {suggestion.CommandName} - {suggestion.Description ?? "<no description>"} [{suggestion.PluginName}]", Color.Yellow);
                }
            }
            AppendToLog("Type 'help' for a full list of available commands.", Color.Yellow);
        }

        private int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s.Length, t.Length];
        }

        private void AppendToLog(string text, Color color)
        {
            logTextBox.SelectionStart = logTextBox.TextLength;
            logTextBox.SelectionColor = color;
            logTextBox.AppendText(text + Environment.NewLine);
            logTextBox.ScrollToCaret();
        }

        private void AppendColoredLine(string[] parts, Color[] colors)
        {
            logTextBox.SelectionStart = logTextBox.TextLength;
            for (int i = 0; i < parts.Length; i++)
            {
                logTextBox.SelectionColor = colors[i];
                logTextBox.AppendText(parts[i]);
            }
            logTextBox.AppendText(Environment.NewLine);
            logTextBox.ScrollToCaret();
        }

        private void Consolehelp()
        {
            AppendToLog("Available commands:", Color.Cyan);
            var coreCommands = _commands
                .Where(c => c.Value.PluginName == "Core")
                .OrderBy(c => c.Key);

            foreach (var command in coreCommands)
            {
                string description = string.IsNullOrEmpty(command.Value.Description) ? "<no description>" : command.Value.Description;
                AppendColoredLine(
                    new[] { $"  {command.Key}", " - ", description, $" [{command.Value.PluginName}]" },
                    new[] { Color.White, Color.Gray, Color.Gray, Color.Cyan }
                );
            }
            var pluginCommands = _commands
                .Where(c => c.Value.PluginName != "Core")
                .OrderBy(c => c.Value.PluginName)
                .ThenBy(c => c.Key);

            if (pluginCommands.Any())
            {
                AppendToLog("----- commands from plugins -----", Color.Cyan);
                foreach (var command in pluginCommands)
                {
                    string commandName = command.Key;
                    var commandBase = command.Key.Split(':').Last();
                    var conflictingCommands = _commands
                        .Count(c => c.Key.EndsWith($":{commandBase}", StringComparison.OrdinalIgnoreCase) || c.Key.Equals(commandBase, StringComparison.OrdinalIgnoreCase));

                    if (conflictingCommands > 1)
                    {
                        commandName = $"{command.Value.PluginName}:{commandBase}";
                    }
                    else
                    {
                        commandName = commandBase;
                    }

                    string description = string.IsNullOrEmpty(command.Value.Description) ? "<no description>" : command.Value.Description;
                    AppendColoredLine(
                        new[] { $"  {commandName}", " - ", description, $" [{command.Value.PluginName}]" },
                        new[] { Color.White, Color.Gray, Color.Gray, Color.Cyan }
                    );
                }
            }
        }

        private void Consoleinfo()
        {
            AppendToLog("                               d8b                           \n                               88P                           \n                              d88                            \n  88bd88b d8888b  88bd8b,d88b 888   d8888b  d8888b ?88,.d88b,\n  88P'  `d8b_,dP  88P'`?8P'?8b?88  d8P' ?88d8P' ?88`?88'  ?88\n d88     88b     d88  d88  88P 88b 88b  d8888b  d88  88b  d8P\nd88'     `?888P'd88' d88'  88b  88b`?8888P'`?8888P'  888888P'\n                                                     88P'    \nby eneko0o lic$andr                                  d88      \n                                                    ?8P      ", Color.Cyan);
            AppendToLog($"â âåðñèè 0.6 äîáàâëåíî:", Color.White);
            AppendToLog($"  êîìàíäû info, pinfo (ïîäðîáíåå 'help')", Color.LightGray);
            AppendToLog($"  óäàëåíèå âåðõíåé ïàíåëè", Color.LightGray);
            AppendToLog($"  îáíîâëåíèå API, ñ äîáàâëåíèåì êîíñòàíòà îïèñàíèÿ êàæäîé çàðåã. êîìàíäû", Color.LightGray);
            AppendToLog($"  äîáàâëåíà ïðîâåðêà íà ðåãèñòðàöèþ ïîõîæèõ ïî èìåíè êîìàíä", Color.LightGray);
            AppendToLog($"  äîáàâëåíà ïðîâåðêà íà ïðàâèëüíîñòü íàçâàíèÿ ïëàãèíà (32 ñèìâîëà, áåç ïðîáåëîâ)", Color.LightGray);
            AppendToLog($"  èñïðàâëåíà îøèáêà, èç-çà êîòîðîé êîìàíäû áåç êîíôëèêòîâ òðåáîâàëè ïðåôèêñ ïëàãèíà", Color.LightGray);
            AppendToLog($"  äîáàâëåíû ïîäñêàçêè äëÿ íåâåðíî ââåäåííûõ êîìàíä (äî 5 ïðåäëîæåíèé, âêëþ÷àÿ êîìàíäû ïëàãèíîâ)", Color.LightGray);
            AppendToLog($"  êîìàíäà versions äëÿ îòîáðàæåíèÿ èñòîðèè âåðñèé", Color.LightGray);
        }

        private void ConsoleInfoHistory()
        {
            AppendToLog("remloop version history:", Color.Cyan);
            AppendToLog($"Version 0.1:", Color.LightGray);
            AppendToLog($"  íà÷àëî ñáîðêè remloop êîíñîëè", Color.Gray);
            AppendToLog($"Version 0.2:", Color.LightGray);
            AppendToLog($"  ñîçäàíèå îôôèöèàëüíûõ ïëàãèíîâ îò ðàçðàáîò÷èêîâ remloop êîíñîëè", Color.Gray);
            AppendToLog($"  ðàñøèðåíèå API", Color.Gray);
            AppendToLog($"  èçìåíåí äèçàéí êîíñîëè", Color.Gray);
            AppendToLog($"Version 0.2.1:", Color.LightGray);
            AppendToLog($"  ðàñøèðåíèå API ñ ñïîñîáîì ïîäêëþ÷åíèÿ áèáëèîòåê", Color.Gray);
            AppendToLog($"Version 0.4:", Color.LightGray);
            AppendToLog($"  ðàñøèðåíèå API", Color.Gray);
            AppendToLog($"  ðàñøèðåíèå äîáàâëåíû êîìàíäû äëÿ âûõîäà (q, quit, close)", Color.Gray);
            AppendToLog($"  óëó÷øåíî ñîîáùåíèå ïðè ââîäå íåâåðíîé êîìàíäû", Color.Gray);
            AppendToLog($"Version 0.6:", Color.LightGray);
            AppendToLog($"  êîìàíäà info", Color.Gray);
            AppendToLog($"  êîìàíäà pinfo", Color.Gray);
            AppendToLog($"  óäàëåíèå âåðõíåé ïàíåëè", Color.Gray);
            AppendToLog($"  îáíîâëåíèå API, ñ äîáàâëåíèåì êîíñòàíòà îïèñàíèÿ êàæäîé çàðåã. êîìàíäû", Color.Gray);
            AppendToLog($"  äîáàâëåíà ïðîâåðêà íà ðåãèñòðàöèþ ïîõîæèõ ïî èìåíè êîìàíä", Color.Gray);
            AppendToLog($"  äîáàâëåíà ïðîâåðêà íà ïðàâèëüíîñòü íàçâàíèÿ ïëàãèíà (32 ñèìâîëà, áåç ïðîáåëîâ)", Color.Gray);
            AppendToLog($"  èñïðàâëåíà îøèáêà, èç-çà êîòîðîé êîìàíäû áåç êîíôëèêòîâ òðåáîâàëè ïðåôèêñ ïëàãèíà", Color.Gray);
            AppendToLog($"  äîáàâëåíû ïîäñêàçêè äëÿ íåâåðíî ââåäåííûõ êîìàíä (äî 5 ïðåäëîæåíèé, âêëþ÷àÿ êîìàíäû ïëàãèíîâ)", Color.Gray);
            AppendToLog($"  êîìàíäà info-history äëÿ îòîáðàæåíèÿ èñòîðèè âåðñèé", Color.Gray);
        }

        private void btnClear_Click(object? sender, EventArgs e)
        {
            logTextBox.Clear();
        }

        private void commandTextBox_Enter(object? sender, EventArgs e)
        {
            statusLabel.Text = "Enter a command and press Enter";
        }

        private void commandTextBox_Leave(object? sender, EventArgs e)
        {
            statusLabel.Text = "Ready";
        }

        public class ConsoleApi : IConsoleApi
        {
            private readonly Form1 _form;
            private readonly string _pluginName;

            public ConsoleApi(Form1 form, string pluginName = "Core")
            {
                _form = form;
                _pluginName = pluginName;
            }

            public void RegisterCommand(string command, Action<string[]> handler, string description = null)
            {
                string commandKey = _pluginName == "Core" ? command.ToLower() : $"{_pluginName.ToLower()}:{command.ToLower()}";
                _form._commands[commandKey] = (handler, description, _pluginName);
                _form.AppendToLog($"Registered command: {commandKey}", Color.Cyan);
            }

            public void Log(string message, Color color)
            {
                _form.AppendToLog(message, color);
            }

            public void ClearConsole()
            {
                _form.logTextBox.Clear();
            }
        }

        public class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
            public override Color MenuItemBorder => Color.FromArgb(40, 40, 40);
            public override Color ToolStripDropDownBackground => Color.FromArgb(40, 40, 40);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCAPTION = 0x2;

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                m.Result = (IntPtr)HTCAPTION;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
