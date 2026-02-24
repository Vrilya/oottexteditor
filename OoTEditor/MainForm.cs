using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OoTEditor;

public class MainForm : Form
{
    // --------------------------------------------------------
    // Data
    // --------------------------------------------------------

    private static readonly string[] TypeNames =
        ["Black", "Wood", "Blue", "Ocarina", "None", "None (black text)"];

    private static readonly string[] PositionNames =
        ["Auto", "Top", "Middle", "Bottom"];

    private List<MessageEntry> _entries = new();
    private int _currentIdx = -1;
    private bool _updating = false;
    private string? _tblPath;
    private string? _binPath;

    // --------------------------------------------------------
    // UI controls
    // --------------------------------------------------------

    private MenuStrip _menuStrip = null!;
    private SplitContainer _split = null!;
    private ListBox _listBox = null!;
    private TextBox _idBox = null!;
    private ComboBox _typeCombo = null!;
    private ComboBox _posCombo = null!;
    private RichTextBox _textEditor = null!;
    private Label _statusLabel = null!;

    // --------------------------------------------------------
    // Constructor
    // --------------------------------------------------------

    public MainForm()
    {
        Text = "OoT Text Editor";
        Size = new Size(920, 620);
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();

        // Set splitter values AFTER the form is fully loaded and has correct dimensions
        Load += (s, e) =>
        {
            _split.Panel1MinSize = 200;
            _split.Panel2MinSize = 400;
            _split.SplitterDistance = 270;
        };
    }

    // --------------------------------------------------------
    // UI construction
    // --------------------------------------------------------

    private void BuildUI()
    {
        // ---------- Status bar (bottom) ----------
        _statusLabel = new Label
        {
            Text = "Load a table (.tbl) and data (.bin) file to begin.",
            Dock = DockStyle.Bottom,
            BorderStyle = BorderStyle.Fixed3D,
            Padding = new Padding(4, 2, 0, 2),
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(_statusLabel);

        // ---------- Menu strip (top) ----------
        _menuStrip = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("File");

        var menuLoad = new ToolStripMenuItem("Load");
        menuLoad.Click += OnLoadFiles;

        var menuSave = new ToolStripMenuItem("Save");
        menuSave.Click += OnSaveFiles;

        var menuSaveAs = new ToolStripMenuItem("Save As...");
        menuSaveAs.Click += OnSaveAsFiles;

        var menuExit = new ToolStripMenuItem("Exit");
        menuExit.Click += (s, e) => Application.Exit();

        fileMenu.DropDownItems.Add(menuLoad);
        fileMenu.DropDownItems.Add(menuSave);
        fileMenu.DropDownItems.Add(menuSaveAs);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(menuExit);

        _menuStrip.Items.Add(fileMenu);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;

        // ---------- SplitContainer (list | editor) ----------
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
        };
        Controls.Add(_split);
        _split.BringToFront();

        // ---------- Left panel: message list ----------
        _listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Courier New", 9.5f),
            IntegralHeight = false,
            ScrollAlwaysVisible = true,
        };
        _listBox.SelectedIndexChanged += OnListSelect;
        _split.Panel1.Controls.Add(_listBox);

        // ---------- Right panel: editor ----------
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(6),
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _split.Panel2.Controls.Add(rightPanel);

        // -- Meta row (ID / Type / Position) --
        var metaPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4),
        };

        metaPanel.Controls.Add(new Label { Text = "ID:", AutoSize = true, Margin = new Padding(0, 5, 2, 0) });

        _idBox = new TextBox
        {
            Width = 70,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Margin = new Padding(0, 2, 12, 0),
        };
        metaPanel.Controls.Add(_idBox);

        metaPanel.Controls.Add(new Label { Text = "Type:", AutoSize = true, Margin = new Padding(0, 5, 2, 0) });

        _typeCombo = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 12, 0),
        };
        _typeCombo.Items.AddRange(TypeNames);
        _typeCombo.SelectedIndexChanged += OnTypeChange;
        metaPanel.Controls.Add(_typeCombo);

        metaPanel.Controls.Add(new Label { Text = "Position:", AutoSize = true, Margin = new Padding(0, 5, 2, 0) });

        _posCombo = new ComboBox
        {
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 0, 0),
        };
        _posCombo.Items.AddRange(PositionNames);
        _posCombo.SelectedIndexChanged += OnPosChange;
        metaPanel.Controls.Add(_posCombo);

        rightPanel.Controls.Add(metaPanel, 0, 0);

        // -- Label above text editor --
        rightPanel.Controls.Add(new Label
        {
            Text = "Message text:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 0, 1);

        // -- Text editor --
        _textEditor = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Courier New", 10.5f),
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            AcceptsTab = false,
        };
        _textEditor.TextChanged += OnTextChanged;
        rightPanel.Controls.Add(_textEditor, 0, 2);
    }

    // --------------------------------------------------------
    // File I/O
    // --------------------------------------------------------

    private void OnLoadFiles(object? sender, EventArgs e)
    {
        using var tblDlg = new OpenFileDialog
        {
            Title = "Select message table file",
            Filter = "Table files (*.tbl)|*.tbl|All files (*.*)|*.*",
        };
        if (tblDlg.ShowDialog() != DialogResult.OK) return;

        using var binDlg = new OpenFileDialog
        {
            Title = "Select message data file",
            Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
        };
        if (binDlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            byte[] tblData = File.ReadAllBytes(tblDlg.FileName);
            byte[] binData = File.ReadAllBytes(binDlg.FileName);

            _entries = MessageCodec.ParseTable(tblData, binData);
            _tblPath = tblDlg.FileName;
            _binPath = binDlg.FileName;
            PopulateList();
            SetStatus($"Loaded {_entries.Count} messages.");

            if (_entries.Count > 0)
            {
                _listBox.SelectedIndex = 0;
                ShowEntry(0);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load files:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSaveFiles(object? sender, EventArgs e)
    {
        if (_entries.Count == 0 || _tblPath == null || _binPath == null)
        {
            MessageBox.Show("No messages loaded.", "Nothing to save",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CommitCurrent();
        WriteFiles(_tblPath, _binPath);
    }

    private void OnSaveAsFiles(object? sender, EventArgs e)
    {
        if (_entries.Count == 0)
        {
            MessageBox.Show("No messages loaded.", "Nothing to save",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CommitCurrent();

        using var tblDlg = new SaveFileDialog
        {
            Title = "Save message table",
            DefaultExt = "tbl",
            Filter = "Table files (*.tbl)|*.tbl|All files (*.*)|*.*",
            FileName = Path.GetFileName(_tblPath) ?? "message_table.tbl",
        };
        if (tblDlg.ShowDialog() != DialogResult.OK) return;

        using var binDlg = new SaveFileDialog
        {
            Title = "Save message data",
            DefaultExt = "bin",
            Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
            FileName = Path.GetFileName(_binPath) ?? "nes_message_data_static.bin",
        };
        if (binDlg.ShowDialog() != DialogResult.OK) return;

        if (WriteFiles(tblDlg.FileName, binDlg.FileName))
        {
            _tblPath = tblDlg.FileName;
            _binPath = binDlg.FileName;
        }
    }

    private bool WriteFiles(string tblPath, string binPath)
    {
        try
        {
            var (tblBytes, msgBytes) = MessageCodec.BuildFiles(_entries);
            File.WriteAllBytes(tblPath, tblBytes);
            File.WriteAllBytes(binPath, msgBytes);
            SetStatus($"Saved to {Path.GetFileName(tblPath)} and {Path.GetFileName(binPath)}.");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    // --------------------------------------------------------
    // List helpers
    // --------------------------------------------------------

    private void PopulateList()
    {
        _listBox.BeginUpdate();
        _listBox.Items.Clear();
        foreach (var entry in _entries)
            _listBox.Items.Add(entry.Label());
        _listBox.EndUpdate();
    }

    private void RefreshListItem(int idx)
    {
        _listBox.Items[idx] = _entries[idx].Label();
        _listBox.SelectedIndex = idx;
    }

    // --------------------------------------------------------
    // Event handlers
    // --------------------------------------------------------

    private void OnListSelect(object? sender, EventArgs e)
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0 || idx == _currentIdx) return;
        CommitCurrent();
        ShowEntry(idx);
    }

    private void ShowEntry(int idx)
    {
        _updating = true;
        var entry = _entries[idx];
        _currentIdx = idx;

        _idBox.Text = $"0x{entry.Id:x4}";

        _typeCombo.SelectedIndex = (entry.Type < TypeNames.Length) ? entry.Type : -1;
        _posCombo.SelectedIndex = (entry.Position < PositionNames.Length) ? entry.Position : -1;

        _textEditor.Text = MessageCodec.ToDisplay(entry.Text);

        SetStatus($"Editing message 0x{entry.Id:x4}  ({idx + 1} / {_entries.Count})");
        _updating = false;
    }

    private void CommitCurrent()
    {
        if (_currentIdx < 0 || _currentIdx >= _entries.Count) return;
        _entries[_currentIdx].Text = MessageCodec.FromDisplay(_textEditor.Text);
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_updating || _currentIdx < 0) return;

        string raw = MessageCodec.FromDisplay(_textEditor.Text);
        _entries[_currentIdx].Text = raw;

        // Re-normalize the display if it differs (forces [break]/[breakdelay] onto own lines)
        string normalized = MessageCodec.ToDisplay(raw);
        if (normalized != _textEditor.Text)
        {
            _updating = true;
            int caret = _textEditor.SelectionStart;
            _textEditor.Text = normalized;
            _textEditor.SelectionStart = Math.Min(caret, _textEditor.Text.Length);
            _updating = false;
        }

        _listBox.SelectedIndexChanged -= OnListSelect;
        RefreshListItem(_currentIdx);
        _listBox.SelectedIndexChanged += OnListSelect;
    }

    private void OnTypeChange(object? sender, EventArgs e)
    {
        if (_updating || _currentIdx < 0) return;
        _entries[_currentIdx].Type = _typeCombo.SelectedIndex;
    }

    private void OnPosChange(object? sender, EventArgs e)
    {
        if (_updating || _currentIdx < 0) return;
        _entries[_currentIdx].Position = _posCombo.SelectedIndex;
    }

    // --------------------------------------------------------
    // Helpers
    // --------------------------------------------------------

    private void SetStatus(string message) => _statusLabel.Text = message;
}