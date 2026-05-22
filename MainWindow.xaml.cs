using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FerrariX;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _pollTimer;
    private bool _xenoInitialized;
    private bool _editorReady;

    public MainWindow()
    {
        InitializeComponent();
        _ = InitEditorAsync();

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += (_, _) => RefreshStatus();
        _pollTimer.Start();

        InitXeno();
        RefreshStatus();
    }

    // ── Inicialización ──────────────────────────────────────────────

    private void InitXeno()
    {
        if (!XenoAPI.DllFound)
        {
            SetStatusBar("Xeno.dll no encontrado — colócalo junto a FerrariX.exe");
            return;
        }
        try
        {
            XenoAPI.Init();
            _xenoInitialized = true;
        }
        catch (Exception ex)
        {
            SetStatusBar($"Error cargando Xeno: {ex.Message}");
        }
    }

    private async Task InitEditorAsync()
    {
        try
        {
            await MonacoEditor.EnsureCoreWebView2Async();
            MonacoEditor.NavigateToString(EditorHtml());
            _editorReady = true;
        }
        catch (Exception ex)
        {
            SetStatusBar($"Error iniciando editor: {ex.Message}");
        }
    }

    // ── Estado / polling ────────────────────────────────────────────

    private void RefreshStatus()
    {
        if (!XenoAPI.DllFound)
        {
            SetConnected(false, "Xeno.dll no encontrado");
            ClientsText.Text = string.Empty;
            return;
        }

        if (!_xenoInitialized)
        {
            SetConnected(false, "Xeno no inicializado");
            return;
        }

        var clients = XenoAPI.GetClientList();

        if (clients.Count > 0)
        {
            SetConnected(true, $"Roblox adjuntado — {clients.Count} cliente(s)");
            ClientsText.Text = $"Clientes: {clients.Count}";
        }
        else if (XenoAPI.RobloxRunning)
        {
            SetConnected(false, "Roblox abierto, esperando attach...");
            ClientsText.Text = string.Empty;
        }
        else
        {
            SetConnected(false, "Roblox no está abierto");
            ClientsText.Text = string.Empty;
        }
    }

    private void SetConnected(bool connected, string message)
    {
        StatusDot.Fill = connected
            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        StatusLabel.Text = connected ? "Conectado" : "Sin Conexión";
        SetStatusBar(message);
    }

    private void SetStatusBar(string msg) => StatusText.Text = msg;

    // ── Botones ─────────────────────────────────────────────────────

    private async void ExecuteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_xenoInitialized)
        {
            SetStatusBar("Xeno.dll no cargado");
            return;
        }

        string? script = await GetScript();
        if (string.IsNullOrWhiteSpace(script))
        {
            SetStatusBar("El editor está vacío");
            return;
        }

        try
        {
            var clients = XenoAPI.GetClientList();
            if (clients.Count == 0)
            {
                SetStatusBar("Sin clientes activos — abre Roblox primero");
                return;
            }

            string[] names = clients.Select(c => c.name).ToArray();
            XenoAPI.ExecuteScript(script, names);
            SetStatusBar($"Script ejecutado en {clients.Count} cliente(s)");
        }
        catch (Exception ex)
        {
            SetStatusBar($"Error al ejecutar: {ex.Message}");
        }
    }

    private async void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_editorReady) return;
        await MonacoEditor.CoreWebView2.ExecuteScriptAsync("clearScript()");
    }

    private async void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_editorReady) return;

        var dlg = new OpenFileDialog
        {
            Title = "Abrir Script",
            Filter = "Archivos Lua (*.lua)|*.lua|Texto (*.txt)|*.txt|Todos (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        string content = await File.ReadAllTextAsync(dlg.FileName);
        string json = System.Text.Json.JsonSerializer.Serialize(content);
        await MonacoEditor.CoreWebView2.ExecuteScriptAsync($"setScript({json})");
        SetStatusBar($"Cargado: {Path.GetFileName(dlg.FileName)}");
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        string? content = await GetScript();
        if (content is null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Guardar Script",
            Filter = "Archivo Lua (*.lua)|*.lua|Texto (*.txt)|*.txt",
            DefaultExt = "lua"
        };

        if (dlg.ShowDialog() != true) return;

        await File.WriteAllTextAsync(dlg.FileName, content);
        SetStatusBar($"Guardado: {Path.GetFileName(dlg.FileName)}");
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private async Task<string?> GetScript()
    {
        if (!_editorReady) return null;
        try
        {
            string raw = await MonacoEditor.CoreWebView2.ExecuteScriptAsync("getScript()");
            return System.Text.Json.JsonSerializer.Deserialize<string>(raw);
        }
        catch { return null; }
    }

    // ── HTML del editor ─────────────────────────────────────────────

    private static string EditorHtml() => """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                html, body { width: 100%; height: 100%; overflow: hidden; background: #0d1117; }
                #editor { width: 100%; height: 100%; }
            </style>
        </head>
        <body>
            <div id="editor"></div>
            <script src="https://cdn.jsdelivr.net/npm/monaco-editor@0.45.0/min/vs/loader.js"></script>
            <script>
                require.config({ paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.45.0/min/vs' } });
                require(['vs/editor/editor.main'], function () {
                    monaco.editor.defineTheme('ferrariDark', {
                        base: 'vs-dark',
                        inherit: true,
                        rules: [
                            { token: 'comment',  foreground: '6b7280', fontStyle: 'italic' },
                            { token: 'keyword',  foreground: 'ff3333' },
                            { token: 'string',   foreground: '86efac' },
                            { token: 'number',   foreground: 'fb923c' },
                            { token: 'operator', foreground: 'c084fc' }
                        ],
                        colors: {
                            'editor.background':              '#0d1117',
                            'editor.lineHighlightBackground': '#161b22',
                            'editorLineNumber.foreground':    '#4b5563',
                            'editor.selectionBackground':     '#cc000040',
                            'editorCursor.foreground':        '#ff2222',
                            'scrollbarSlider.background':     '#30363d80'
                        }
                    });

                    window.editor = monaco.editor.create(document.getElementById('editor'), {
                        value: '-- FerrariX | Escribe tu script aquí\n',
                        language: 'lua',
                        theme: 'ferrariDark',
                        fontSize: 14,
                        fontFamily: 'Consolas, "Courier New", monospace',
                        minimap: { enabled: false },
                        scrollBeyondLastLine: false,
                        automaticLayout: true,
                        lineNumbers: 'on',
                        renderLineHighlight: 'line',
                        tabSize: 4,
                        wordWrap: 'off'
                    });
                });

                function getScript()   { return window.editor ? window.editor.getValue() : ''; }
                function setScript(v)  { if (window.editor) window.editor.setValue(v); }
                function clearScript() { if (window.editor) window.editor.setValue(''); }
            </script>
        </body>
        </html>
        """;
}
