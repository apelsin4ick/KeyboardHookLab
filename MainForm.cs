using System.ComponentModel;
using System.Diagnostics;
using KeyboardHookLab.Interop;

namespace KeyboardHookLab;

public sealed class MainForm : Form
{
    private readonly TextBox _logTextBox;
    private readonly CheckBox _hookCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _counterLabel;
    private readonly Button _clearButton;
    private readonly KeyboardHook _keyboardHook;
    private int _eventCount;
    private bool _closing;

    public MainForm()
    {
        Text = "П2 — Keyboard Hook Laboratory";
        Width = 900;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyPressed += OnHookKeyPressed;

        _logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical
        };

        _hookCheckBox = new CheckBox
        {
            Text = "Включить low-level hook",
            AutoSize = true,
            Margin = new Padding(8)
        };

        _clearButton = new Button
        {
            Text = "Очистить журнал",
            AutoSize = true
        };

        _statusLabel = new Label
        {
            Text = "Hook не установлен",
            AutoSize = true,
            Margin = new Padding(8)
        };

        _counterLabel = new Label
        {
            Text = "Событий: 0",
            AutoSize = true,
            Margin = new Padding(8)
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false
        };

        toolbar.Controls.Add(_hookCheckBox);
        toolbar.Controls.Add(_clearButton);
        toolbar.Controls.Add(_statusLabel);
        toolbar.Controls.Add(_counterLabel);

        Controls.Add(_logTextBox);
        Controls.Add(toolbar);

        KeyDown += OnLocalKeyDown;
        _hookCheckBox.CheckedChanged += OnHookCheckedChanged;
        _clearButton.Click += (_, _) => ClearLog();

        Activated += OnFormActivated;
        Deactivate += OnFormDeactivated;
        FormClosing += OnFormClosing;
    }

    private void OnLocalKeyDown(object? sender, KeyEventArgs e)
    {
        AppendLog("WinForms", e.KeyCode.ToString());
    }

    private void OnHookKeyPressed(Keys key)
    {
        if (_closing || IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!_closing && !IsDisposed)
                    AppendLog("Low-level hook", key.ToString());
            }));
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Не удалось обновить журнал: {ex}");
        }
    }

    private void AppendLog(string source, string key)
    {
        _logTextBox.AppendText(
            $"[{DateTimeOffset.Now:HH:mm:ss.fff}] [{source}] {key}" +
            Environment.NewLine);

        _eventCount++;
        UpdateCounter();
    }

    private void ClearLog()
    {
        _logTextBox.Clear();
        _eventCount = 0;
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        _counterLabel.Text = $"Событий: {_eventCount}";
    }

    private void OnHookCheckedChanged(object? sender, EventArgs e)
    {
        if (_hookCheckBox.Checked)
        {
            if (ContainsFocus)
                StartHook();
        }
        else
        {
            StopHook();
        }
    }

    private void OnFormActivated(object? sender, EventArgs e)
    {
        if (_hookCheckBox.Checked)
            StartHook();
    }

    private void OnFormDeactivated(object? sender, EventArgs e)
    {
        StopHook();
    }

    private void StartHook()
    {
        if (_closing || _keyboardHook.IsInstalled)
            return;

        try
        {
            _keyboardHook.Start();
            _statusLabel.Text = "Hook установлен";
            AppendLog("System", "Hook установлен");
        }
        catch (Win32Exception ex)
        {
            _statusLabel.Text = "Ошибка установки hook";
            AppendLog("Error", ex.Message);
            _hookCheckBox.Checked = false;
        }
    }

    private void StopHook()
    {
        if (!_keyboardHook.IsInstalled)
        {
            _statusLabel.Text = "Hook не установлен";
            return;
        }

        try
        {
            _keyboardHook.Stop();
            _statusLabel.Text = "Hook не установлен";
            AppendLog("System", "Hook снят");
        }
        catch (Win32Exception ex)
        {
            _statusLabel.Text = "Ошибка снятия hook";
            AppendLog("Error", ex.Message);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _closing = true;
        _hookCheckBox.Enabled = false;
        _clearButton.Enabled = false;

        try
        {
            _keyboardHook.Dispose();
            _statusLabel.Text = "Hook снят";
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"Ошибка освобождения hook: {ex}");
        }
    }
}
