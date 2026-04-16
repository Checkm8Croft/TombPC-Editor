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
        // Script-specific lists
        private List<StringEntry> _levelStrings = new();
        private List<StringEntry> _chapterStrings = new();
        private List<StringEntry> _titleStrings = new();
        private List<StringEntry> _fmvStrings = new();
        private List<StringEntry> _levelPathStrings = new();
        private List<StringEntry> _cutscenePathStrings = new();
        private bool _isTombPcDat = false;
        private byte _xorKey = 0;
        private bool _scriptUseXor = false;
        private LogWindow _logWindow;

        private class StringEntry
        {
            public long Offset { get; set; }
            public int Length { get; set; } // including terminating 0
            public string Text { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
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
            AppendLog("Entering ParseTombPcDat()");
            using var ms = new MemoryStream(_fileBytes);
            using var br = new BinaryReader(ms, Encoding.ASCII);

            uint version = br.ReadUInt32();
            AppendLog($"Version: 0x{version:X8}");

            // description (256)
            var descBytes = br.ReadBytes(256);
            string description = Encoding.ASCII.GetString(descBytes).TrimEnd('\0');
            AppendLog($"Description (trim): {description.Substring(0, Math.Min(120, description.Length))}");

            ushort gameflowSize = br.ReadUInt16();
            AppendLog($"GameflowSize: {gameflowSize}");

            // next are several int32/uint32 fields
            int FirstOption = br.ReadInt32();
            int TitleReplace = br.ReadInt32();
            int OnDeathDemoMode = br.ReadInt32();
            int OnDeathInGame = br.ReadInt32();
            uint DemoTime = br.ReadUInt32();
            int OnDemoInterrupt = br.ReadInt32();
            int OnDemoEnd = br.ReadInt32();

            // Unknown1 36 bytes
            br.ReadBytes(36);

            ushort NumLevels = br.ReadUInt16();
            ushort NumChapterScreens = br.ReadUInt16();
            ushort NumTitles = br.ReadUInt16();
            ushort NumFMVs = br.ReadUInt16();
            ushort NumCutscenes = br.ReadUInt16();
            ushort NumDemoLevels = br.ReadUInt16();
            ushort TitleSoundID = br.ReadUInt16();
            ushort SingleLevel = br.ReadUInt16();

            AppendLog("");
            AppendLog("=== String array counts ===");
            AppendLog($"NumLevels={NumLevels}, NumChapterScreens={NumChapterScreens}, NumTitles={NumTitles}");
            AppendLog($"NumFMVs={NumFMVs}, NumCutscenes={NumCutscenes}, NumDemoLevels={NumDemoLevels}");

            // Unknown2 32 bytes
            br.ReadBytes(32);

            ushort Flags = br.ReadUInt16();
            br.ReadBytes(6); // Unknown3
            byte XORKey = br.ReadByte();
            byte LanguageID = br.ReadByte();
            ushort SecretSoundID = br.ReadUInt16();
            br.ReadBytes(4); // Unknown4

            bool useXor = (Flags & 0x0100) != 0; // bit 8
            _xorKey = XORKey;
            _scriptUseXor = useXor;

            AppendLog("");
            AppendLog("=== Flags and encryption ===");
            AppendLog($"Flags=0x{Flags:X4}, useXor={useXor}, XORKey=0x{XORKey:X2}, LanguageID={LanguageID}");

            // Helper to read TPCStringArray (per TRosettaStone spec)
            List<StringEntry> ReadStringArray(int count, string arrayName)
            {
                var result = new List<StringEntry>();
                if (count == 0)
                {
                    AppendLog($"[{arrayName}] count=0, skipping");
                    return result;
                }

                long startPos = br.BaseStream.Position;
                AppendLog($"");
                AppendLog($"[{arrayName}] Starting read at offset 0x{startPos:X}, count={count}");

                // Read offset table (always uint16)
                ushort[] offsets = new ushort[count];
                for (int i = 0; i < count; i++) offsets[i] = br.ReadUInt16();

                AppendLog($"  First offsets sample: {string.Join(",", offsets.Take(Math.Min(8, offsets.Length)))}");

                // Read total size (always uint16 per spec)
                ushort totalSize = br.ReadUInt16();
                AppendLog($"  totalSize={totalSize}");

                // **SANITY CHECK**: If all offsets are impossibly large, the data is likely garbage
                if (count > 0 && offsets.All(o => o >= 1000) && totalSize < 1000)
                {
                    AppendLog($"  ⚠ All offsets are impossibly large (min={offsets.Min()}, max={offsets.Max()}) vs totalSize={totalSize}");
                    AppendLog($"  Likely invalid/corrupted data. Skipping this array.");
                    br.ReadBytes(totalSize);
                    return result;
                }

                if (totalSize == 0)
                {
                    AppendLog($"  Empty array (totalSize=0), returning 0 strings");
                    return result;
                }

                // **BOUNDARY CHECK**: Verify we have enough bytes in the file
                long bytesAvailable = ms.Length - br.BaseStream.Position;
                if (totalSize > bytesAvailable)
                {
                    AppendLog($"  ⚠ WARNING: totalSize={totalSize} exceeds available bytes={bytesAvailable}");
                    AppendLog($"  File appears truncated or data format is incorrect. Skipping this array.");
                    // Skip what we can
                    br.ReadBytes((int)Math.Min(bytesAvailable, totalSize));
                    return result;
                }

                // Read data block
                long dataStart = br.BaseStream.Position;
                byte[] data = br.ReadBytes(totalSize);

                // Decrypt if needed
                if (useXor)
                {
                    AppendLog($"  Decrypting with XOR key 0x{XORKey:X2}...");
                    for (int i = 0; i < data.Length; i++) data[i] ^= XORKey;
                }

                AppendLog($"  data.Length={data.Length}");

                // Extract strings using offset table
                for (int i = 0; i < count; i++)
                {
                    int off = offsets[i];
                    if (off >= data.Length)
                    {
                        AppendLog($"  ⚠ String[{i}] offset 0x{off:X} beyond data");
                        continue;
                    }

                    int len = 0;
                    while (off + len < data.Length && data[off + len] != 0) len++;

                    if (len > 0)
                    {
                        string s = Encoding.ASCII.GetString(data, off, len);
                        var entry = new StringEntry { Offset = dataStart + off, Length = len + 1, Text = s };
                        result.Add(entry);
                    }
                }

                AppendLog($"  ✓ Read {result.Count} strings");
                return result;
            }

            // Read arrays in TR2/TR3 order with error handling
            try { _levelStrings = ReadStringArray(NumLevels, "Levels"); }
            catch (Exception ex) { AppendLog($"⚠ Error reading Levels: {ex.Message}"); }

            try { _chapterStrings = ReadStringArray(NumChapterScreens, "Chapters"); }
            catch (Exception ex) { AppendLog($"⚠ Error reading Chapters: {ex.Message}"); }

            try { _titleStrings = ReadStringArray(NumTitles, "Titles"); }
            catch (Exception ex) { AppendLog($"⚠ Error reading Titles: {ex.Message}"); }

            try { _fmvStrings = ReadStringArray(NumFMVs, "FMVs"); }
            catch (Exception ex) { AppendLog($"⚠ Error reading FMVs: {ex.Message}"); }

            try { _levelPathStrings = ReadStringArray(NumLevels, "LevelPaths"); }
            catch (Exception ex) { AppendLog($"⚠ Error reading LevelPaths: {ex.Message}"); }

            try { _cutscenePathStrings = ReadStringArray(NumCutscenes, "Cutscenes"); }
            catch (Exception ex) { AppendLog($"⚠ Error reading Cutscenes: {ex.Message}"); }

            // Helper to format with index for debugging clarity
            List<string> Format(List<StringEntry> list)
            {
                var outList = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    outList.Add($"[{i}] {list[i].Text}");
                }
                if (outList.Count == 0) outList.Add("<empty>");
                return outList;
            }

            // Populate all ListBox controls with data
            RefreshListBoxes();

            AppendLog("");
            AppendLog("=== Parse result ===");
            Status.Text = $"TOMBPC.DAT parsed. Levels:{_levelStrings.Count} Chapters:{_chapterStrings.Count} Titles:{_titleStrings.Count} FMVs:{_fmvStrings.Count} Paths:{_levelPathStrings.Count} Cutscenes:{_cutscenePathStrings.Count}";
            AppendLog(Status.Text);

            // If parsing produced few strings, warn user
            int totalStrings = _levelStrings.Count + _chapterStrings.Count + _titleStrings.Count + _fmvStrings.Count + _levelPathStrings.Count + _cutscenePathStrings.Count;
            if (totalStrings == 0)
            {
                AppendLog("");
                AppendLog("⚠ WARNING: Parsed header but no string arrays found.");
                AppendLog("Check the log above for offsets, sizes and parsing details.");
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