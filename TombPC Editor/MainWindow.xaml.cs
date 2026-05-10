using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace TombPC_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private byte[] _fileBytes;
        private string _currentFilePath;
        private List<StringEntry> _strings = new();
        // Script-specific lists (Phase 1-3)
        private List<StringEntry> _levelStrings = new();
        private List<StringEntry> _chapterStrings = new();
        private List<StringEntry> _titleStrings = new();
        private List<StringEntry> _fmvStrings = new();
        private List<StringEntry> _levelPathStrings = new();
        private List<StringEntry> _cutscenePathStrings = new();
        // Phase 4: Item strings (indexed by type, each type has NumLevels entries)
        private List<StringEntry>[] _puzzleStrings = new List<StringEntry>[GameflowConstants.NumPuzzleItemsPerLevel];
        private List<StringEntry>[] _pickupStrings = new List<StringEntry>[GameflowConstants.NumPickupsPerLevel];
        private List<StringEntry>[] _keyStrings = new List<StringEntry>[GameflowConstants.NumKeysPerLevel];
        private int _currentPuzzleType = 0;
        private int _currentPickupType = 0;
        private int _currentKeyType = 0;
        private bool _isTombPcDat = false;
        private byte _xorKey = 0;
        private bool _scriptUseXor = false;
        private LogWindow _logWindow;

        // Structured gameflow data
        private Gameflow _gameflow = new();

        public MainWindow()
        {
            InitializeComponent();

            // Initialize Phase 4 array elements
            for (int i = 0; i < GameflowConstants.NumPuzzleItemsPerLevel; i++)
                _puzzleStrings[i] = new List<StringEntry>();
            for (int i = 0; i < GameflowConstants.NumPickupsPerLevel; i++)
                _pickupStrings[i] = new List<StringEntry>();
            for (int i = 0; i < GameflowConstants.NumKeysPerLevel; i++)
                _keyStrings[i] = new List<StringEntry>();
        }


        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "DAT files|*.dat;*.DAT|All files|*.*";
            if (dlg.ShowDialog(this) != true)
                return;

            _currentFilePath = dlg.FileName;
            _fileBytes = File.ReadAllBytes(_currentFilePath);

            // Open log window before parsing
            _logWindow = new LogWindow();
            _logWindow.Owner = this;
            _logWindow.Show();

            try
            {
                ParseStringsFromBytes();
                Status.Text = $"Loaded {_currentFilePath}";
            }
            catch (Exception ex)
            {
                AppendLog("Error during ParseStringsFromBytes: " + ex);
                Status.Text = "Error parsing file; see log.";
            }
        }

        private void AppendLog(string line)
        {
            if (_logWindow != null)
            {
                try
                {
                    _logWindow.AppendLog(line);
                }
                catch { }
            }
        }

        /// <summary>
        /// Converts a TpcStringArray (with binary data) into a List of StringEntry for UI binding.
        /// </summary>
        private List<StringEntry> ConvertTpcToStringEntry(TpcStringArray tpcArray)
        {
            var result = new List<StringEntry>();

            if (tpcArray.Data == null || tpcArray.Count == 0)
                return result;

            byte[] data = tpcArray.Data;
            for (int i = 0; i < tpcArray.Count; i++)
            {
                int offset = tpcArray.Offsets[i];
                if (offset >= data.Length)
                    continue;

                int len = 0;
                while (offset + len < data.Length && data[offset + len] != 0)
                    len++;

                if (len > 0)
                {
                    string text = Encoding.ASCII.GetString(data, offset, len);
                    result.Add(new StringEntry { Offset = offset, Length = len + 1, Text = text });
                }
            }

            return result;
        }

        private void ParseStringsFromBytes()
        {
            AppendLog($"Parse started: {DateTime.Now:O}");
            AppendLog($"Current file: {_currentFilePath}");
            AppendLog($"File size: {_fileBytes.Length} bytes");
            _strings.Clear();
            _levelStrings.Clear();
            _chapterStrings.Clear();
            _titleStrings.Clear();
            _fmvStrings.Clear();
            _levelPathStrings.Clear();
            _cutscenePathStrings.Clear();
            // Clear Phase 4 arrays
            for (int t = 0; t < _puzzleStrings.Length; t++) _puzzleStrings[t].Clear();
            for (int t = 0; t < _pickupStrings.Length; t++) _pickupStrings[t].Clear();
            for (int t = 0; t < _keyStrings.Length; t++) _keyStrings[t].Clear();
            _currentPuzzleType = 0;
            _currentPickupType = 0;
            _currentKeyType = 0;

            if (_fileBytes == null) return;

            // Check for Tomb Raider script header in description (offset 4, 256 bytes)
            if (_fileBytes.Length > 260)
            {
                string desc = Encoding.ASCII.GetString(_fileBytes, 4, Math.Min(256, _fileBytes.Length - 4));
                AppendLog("=== Detected description (first 256 chars) ===");
                AppendLog(desc.Length > 200 ? desc.Substring(0, 200) + "..." : desc);
                if (desc.Contains("Tomb Raider"))
                {
                    try
                    {
                        AppendLog("");
                        AppendLog("=== Attempting TOMBPC.DAT (TR2/TR3) format parser ===");
                        ParseTombPcDat();
                        _isTombPcDat = true;
                        Status.Text = "Detected TOMBPC.DAT script format";
                        return;
                    }
                    catch (Exception ex)
                    {
                        // fall back to heuristic
                        AppendLog($"Script parse failed: {ex.Message}");
                        AppendLog("");
                        AppendLog("=== Falling back to ASCII run extraction ===");
                        Status.Text = "Script parse failed, falling back: " + ex.Message;
                        _isTombPcDat = false;
                    }
                }
            }

            // fallback: simple ascii-run extraction
            AppendLog("=== Using ASCII run extraction (fallback) ===");
            var bytes = _fileBytes;
            int i = 0;
            while (i < bytes.Length)
            {
                if (IsPrintable(bytes[i]))
                {
                    int start = i;
                    var sb = new StringBuilder();
                    while (i < bytes.Length && bytes[i] != 0)
                    {
                        if (!IsPrintable(bytes[i])) break;
                        sb.Append((char)bytes[i]);
                        i++;
                    }
                    if (i < bytes.Length && bytes[i] == 0)
                    {
                        int lenWithTerminator = i - start + 1;
                        if (sb.Length >= 3)
                        {
                            _strings.Add(new StringEntry { Offset = start, Length = lenWithTerminator, Text = sb.ToString() });
                        }
                        i++;
                    }
                    else i = start + 1;
                }
                else i++;
            }

            AppendLog($"Found {_strings.Count} ASCII strings");
            RefreshListBoxes();
        }

        private void ParseTombPcDat()
        {
            AppendLog("=== Parsing TOMBPC.DAT ===");

            var parser = new GameflowParser(AppendLog);
            try
            {
                _gameflow = parser.Parse(_fileBytes);

                // Convert TpcStringArray to StringEntry lists for UI (Phase 1-3)
                _levelStrings = ConvertTpcToStringEntry(_gameflow.LevelStrings);
                _chapterStrings = ConvertTpcToStringEntry(_gameflow.ChapterScreenStrings);
                _titleStrings = ConvertTpcToStringEntry(_gameflow.TitleStrings);
                _fmvStrings = ConvertTpcToStringEntry(_gameflow.FmvStrings);
                _levelPathStrings = ConvertTpcToStringEntry(_gameflow.LevelPathStrings);
                _cutscenePathStrings = ConvertTpcToStringEntry(_gameflow.CutscenePathStrings);

                // Phase 4: Convert item string arrays
                // Each type contains all level strings for that type
                for (int type = 0; type < GameflowConstants.NumPuzzleItemsPerLevel; type++)
                {
                    _puzzleStrings[type] = ConvertTpcToStringEntry(_gameflow.PuzzleStrings[type]);
                }

                for (int type = 0; type < GameflowConstants.NumPickupsPerLevel; type++)
                {
                    _pickupStrings[type] = ConvertTpcToStringEntry(_gameflow.PickupStrings[type]);
                }

                for (int type = 0; type < GameflowConstants.NumKeysPerLevel; type++)
                {
                    _keyStrings[type] = ConvertTpcToStringEntry(_gameflow.KeyStrings[type]);
                }

                // Update encryption flags for compatibility
                _xorKey = _gameflow.XorKey;
                _scriptUseXor = (_gameflow.Flags & GameflowFlags.XorEncryption) != 0;

                // Populate UI
                RefreshListBoxes();
                RefreshComboBoxes();

                // Status message
                int totalStrings = _levelStrings.Count + _chapterStrings.Count + _titleStrings.Count 
                    + _fmvStrings.Count + _levelPathStrings.Count + _cutscenePathStrings.Count;
                Status.Text = totalStrings > 0 
                    ? $"✓ TOMBPC.DAT parsed. Total: {totalStrings} strings"
                    : $"⚠ Parsed header but found no strings.";
            }
            catch (Exception ex)
            {
                AppendLog($"⚠ Parse error: {ex.Message}");
                Status.Text = $"Error: {ex.Message}";
            }
        }

        private static bool IsPrintable(byte b)
        {
            return b >= 0x20 && b <= 0x7E; // basic printable ASCII
        }

        private void RefreshListBoxes()
        {
            // Populate all string array lists
            ListLevelStrings.ItemsSource = _levelStrings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            ListChapterStrings.ItemsSource = _chapterStrings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            ListTitleStrings.ItemsSource = _titleStrings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            ListFMVStrings.ItemsSource = _fmvStrings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            ListLevelPathStrings.ItemsSource = _levelPathStrings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            ListCutscenePathStrings.ItemsSource = _cutscenePathStrings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
        }

        private void RefreshComboBoxes()
        {
            // Populate combo boxes for multi-dimensional arrays
            var puzzleTypes = Enumerable.Range(0, _puzzleStrings.Length)
                .Select(i => $"Puzzle Type {i + 1}")
                .ToList();
            PuzzleTypeCombo.ItemsSource = puzzleTypes;
            if (puzzleTypes.Count > 0)
                PuzzleTypeCombo.SelectedIndex = 0;

            var pickupTypes = Enumerable.Range(0, _pickupStrings.Length)
                .Select(i => $"Pickup Type {i + 1}")
                .ToList();
            PickupTypeCombo.ItemsSource = pickupTypes;
            if (pickupTypes.Count > 0)
                PickupTypeCombo.SelectedIndex = 0;

            var keyTypes = Enumerable.Range(0, _keyStrings.Length)
                .Select(i => $"Key Type {i + 1}")
                .ToList();
            KeyTypeCombo.ItemsSource = keyTypes;
            if (keyTypes.Count > 0)
                KeyTypeCombo.SelectedIndex = 0;
        }

        private void OnPuzzleTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentPuzzleType = PuzzleTypeCombo.SelectedIndex;
            if (_currentPuzzleType >= 0 && _currentPuzzleType < _puzzleStrings.Length)
            {
                var strings = _puzzleStrings[_currentPuzzleType];
                ListPuzzleStrings.ItemsSource = strings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            }
        }

        private void OnPickupTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentPickupType = PickupTypeCombo.SelectedIndex;
            if (_currentPickupType >= 0 && _currentPickupType < _pickupStrings.Length)
            {
                var strings = _pickupStrings[_currentPickupType];
                ListPickupStrings.ItemsSource = strings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            }
        }

        private void OnKeyTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentKeyType = KeyTypeCombo.SelectedIndex;
            if (_currentKeyType >= 0 && _currentKeyType < _keyStrings.Length)
            {
                var strings = _keyStrings[_currentKeyType];
                ListKeyStrings.ItemsSource = strings.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnStringSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox lb)
            {
                int idx = lb.SelectedIndex;
                if (idx < 0) return;

                // Determine which list was selected
                List<StringEntry> activeList = null;
                TextBox activeTextBox = null;
                TextBlock activeLabel = null;
                TextBlock activeInfo = null;

                if (lb == ListLevelStrings)
                {
                    if (idx >= _levelStrings.Count) return;
                    activeList = _levelStrings;
                    activeTextBox = TextEdit;
                    activeLabel = LevelNameLabel;
                    activeInfo = InfoText;
                }
                else if (lb == ListChapterStrings)
                {
                    if (idx >= _chapterStrings.Count) return;
                    activeList = _chapterStrings;
                    activeTextBox = TextEditChapter;
                    activeLabel = ChapterNameLabel;
                    activeInfo = InfoTextChapter;
                }
                else if (lb == ListTitleStrings)
                {
                    if (idx >= _titleStrings.Count) return;
                    activeList = _titleStrings;
                    activeTextBox = TextEditTitle;
                    activeLabel = TitleNameLabel;
                    activeInfo = InfoTextTitle;
                }
                else if (lb == ListFMVStrings)
                {
                    if (idx >= _fmvStrings.Count) return;
                    activeList = _fmvStrings;
                    activeTextBox = TextEditFMV;
                    activeLabel = FMVNameLabel;
                    activeInfo = InfoTextFMV;
                }
                else if (lb == ListLevelPathStrings)
                {
                    if (idx >= _levelPathStrings.Count) return;
                    activeList = _levelPathStrings;
                    activeTextBox = TextEditLevelPath;
                    activeLabel = LevelPathNameLabel;
                    activeInfo = InfoTextLevelPath;
                }
                else if (lb == ListCutscenePathStrings)
                {
                    if (idx >= _cutscenePathStrings.Count) return;
                    activeList = _cutscenePathStrings;
                    activeTextBox = TextEditCutscene;
                    activeLabel = CutsceneNameLabel;
                    activeInfo = InfoTextCutscene;
                }
                else if (lb == ListPuzzleStrings)
                {
                    if (_currentPuzzleType < 0 || _currentPuzzleType >= _puzzleStrings.Length || idx >= _puzzleStrings[_currentPuzzleType].Count) return;
                    activeList = _puzzleStrings[_currentPuzzleType];
                    activeTextBox = TextEditPuzzle;
                    activeLabel = PuzzleNameLabel;
                    activeInfo = InfoTextPuzzle;
                }
                else if (lb == ListPickupStrings)
                {
                    if (_currentPickupType < 0 || _currentPickupType >= _pickupStrings.Length || idx >= _pickupStrings[_currentPickupType].Count) return;
                    activeList = _pickupStrings[_currentPickupType];
                    activeTextBox = TextEditPickup;
                    activeLabel = PickupNameLabel;
                    activeInfo = InfoTextPickup;
                }
                else if (lb == ListKeyStrings)
                {
                    if (_currentKeyType < 0 || _currentKeyType >= _keyStrings.Length || idx >= _keyStrings[_currentKeyType].Count) return;
                    activeList = _keyStrings[_currentKeyType];
                    activeTextBox = TextEditKey;
                    activeLabel = KeyNameLabel;
                    activeInfo = InfoTextKey;
                }

                if (activeList != null && activeTextBox != null)
                {
                    var entry = activeList[idx];
                    activeTextBox.Text = entry.Text;
                    activeLabel.Text = $"String [{idx + 1:00}]: {entry.Text}";
                    activeInfo.Text = $"Offset: 0x{entry.Offset:X} | Length: {entry.Length} bytes";
                }
            }
        }

        private void UpdateString_Click(object sender, RoutedEventArgs e)
        {
            // Find which tab is active and which list/textbox pair to use
            TabItem activeTab = Tabs.SelectedItem as TabItem;
            if (activeTab == null) return;

            List<StringEntry> activeList = null;
            TextBox activeTextBox = null;
            ListBox activeListBox = null;
            string categoryName = "";

            // Determine which tab is active
            if (Tabs.SelectedIndex == 0) // Level Strings
            {
                activeList = _levelStrings;
                activeTextBox = TextEdit;
                activeListBox = ListLevelStrings;
                categoryName = "Level";
            }
            else if (Tabs.SelectedIndex == 1) // Chapter Strings
            {
                activeList = _chapterStrings;
                activeTextBox = TextEditChapter;
                activeListBox = ListChapterStrings;
                categoryName = "Chapter";
            }
            else if (Tabs.SelectedIndex == 2) // Title Strings
            {
                activeList = _titleStrings;
                activeTextBox = TextEditTitle;
                activeListBox = ListTitleStrings;
                categoryName = "Title";
            }
            else if (Tabs.SelectedIndex == 3) // FMV Strings
            {
                activeList = _fmvStrings;
                activeTextBox = TextEditFMV;
                activeListBox = ListFMVStrings;
                categoryName = "FMV";
            }
            else if (Tabs.SelectedIndex == 4) // Level Path Strings
            {
                activeList = _levelPathStrings;
                activeTextBox = TextEditLevelPath;
                activeListBox = ListLevelPathStrings;
                categoryName = "LevelPath";
            }
            else if (Tabs.SelectedIndex == 5) // Cutscene Path Strings
            {
                activeList = _cutscenePathStrings;
                activeTextBox = TextEditCutscene;
                activeListBox = ListCutscenePathStrings;
                categoryName = "Cutscene";
            }
            else if (Tabs.SelectedIndex == 6) // Puzzle Strings
            {
                if (_currentPuzzleType >= 0 && _currentPuzzleType < _puzzleStrings.Length)
                {
                    activeList = _puzzleStrings[_currentPuzzleType];
                    activeTextBox = TextEditPuzzle;
                    activeListBox = ListPuzzleStrings;
                    categoryName = $"Puzzle Type {_currentPuzzleType + 1}";
                }
            }
            else if (Tabs.SelectedIndex == 7) // Pickup Strings
            {
                if (_currentPickupType >= 0 && _currentPickupType < _pickupStrings.Length)
                {
                    activeList = _pickupStrings[_currentPickupType];
                    activeTextBox = TextEditPickup;
                    activeListBox = ListPickupStrings;
                    categoryName = $"Pickup Type {_currentPickupType + 1}";
                }
            }
            else if (Tabs.SelectedIndex == 8) // Key Strings
            {
                if (_currentKeyType >= 0 && _currentKeyType < _keyStrings.Length)
                {
                    activeList = _keyStrings[_currentKeyType];
                    activeTextBox = TextEditKey;
                    activeListBox = ListKeyStrings;
                    categoryName = $"Key Type {_currentKeyType + 1}";
                }
            }

            if (activeList == null || activeTextBox == null)
            {
                Status.Text = "No active list selected.";
                return;
            }

            int idx = activeListBox.SelectedIndex;
            if (idx < 0 || idx >= activeList.Count)
            {
                Status.Text = "No string selected.";
                return;
            }

            var entry = activeList[idx];
            string newText = activeTextBox.Text ?? string.Empty;
            var newBytes = Encoding.ASCII.GetBytes(newText);

            if (newBytes.Length + 1 > entry.Length)
            {
                MessageBox.Show(this, $"New text too long! Max {entry.Length - 1} characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Apply change in buffer
            Array.Copy(newBytes, 0, _fileBytes, entry.Offset, newBytes.Length);
            int termPos = (int)entry.Offset + newBytes.Length;
            _fileBytes[termPos] = 0;
            for (long i = termPos + 1; i < entry.Offset + entry.Length; i++) _fileBytes[i] = 0;

            entry.Text = newText;
            activeListBox.ItemsSource = activeList.Select((x, i) => $"[{i + 1:00}] {x.Text}").ToList();
            activeListBox.SelectedIndex = idx;

            Status.Text = $"✓ Updated {categoryName} string [{idx + 1}]";
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (_fileBytes == null)
            {
                Status.Text = "No file loaded.";
                return;
            }
            var dlg = new SaveFileDialog();
            dlg.Filter = "DAT files|*.dat;*.DAT|All files|*.*";
            dlg.FileName = Path.GetFileName(_currentFilePath ?? "patched.dat");
            if (dlg.ShowDialog(this) != true) return;
            File.WriteAllBytes(dlg.FileName, _fileBytes);
            Status.Text = $"Saved to {dlg.FileName}";
        }
    }
}