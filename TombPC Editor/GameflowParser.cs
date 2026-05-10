using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TombPC_Editor
{
    /// <summary>
    /// TOMBPC.DAT Parser following TRosettaStone specification.
    /// Implements the pattern from GameflowReader.cs with method-per-field approach
    /// for better maintainability and error handling.
    /// </summary>
    public class GameflowParser
    {
        private BinaryReader _reader;
        private MemoryStream _stream;
        private bool _useXor;
        private Action<string> _logger;

        public GameflowParser(Action<string> logger = null)
        {
            _logger = logger ?? (msg => { });
        }

        /// <summary>
        /// Main entry point: parses a TOMBPC.DAT file from a byte array.
        /// </summary>
        public Gameflow Parse(byte[] fileBytes)
        {
            _stream = new MemoryStream(fileBytes);
            _reader = new BinaryReader(_stream, Encoding.ASCII);

            try
            {
                var gameflow = new Gameflow();

                Log("=== TOMBPC.DAT Parser Started ===");
                Log($"File size: {fileBytes.Length} bytes");

                // Header section
                ReadVersion(_reader, gameflow);
                ReadDescription(_reader, gameflow);
                ReadGameflowSize(_reader, gameflow);

                // Configuration section (int32/uint32 fields)
                ReadFirstOption(_reader, gameflow);
                ReadTitleReplace(_reader, gameflow);
                ReadOnDeathDemoMode(_reader, gameflow);
                ReadOnDeathInGame(_reader, gameflow);
                ReadDemoTime(_reader, gameflow);
                ReadOnDemoInterrupt(_reader, gameflow);
                ReadOnDemoEnd(_reader, gameflow);
                SkipBytes(_reader, GameflowConstants.Unknown1Length, "Unknown1");

                // String array counts
                ReadNumLevels(_reader, gameflow);
                ReadNumChapterScreens(_reader, gameflow);
                ReadNumTitles(_reader, gameflow);
                ReadNumFmvs(_reader, gameflow);
                ReadNumCutscenes(_reader, gameflow);
                ReadNumDemoLevels(_reader, gameflow);
                ReadTitleSoundId(_reader, gameflow);
                ReadSingleLevel(_reader, gameflow);

                SkipBytes(_reader, GameflowConstants.Unknown2Length, "Unknown2");

                // Flags and encryption config
                ReadFlags(_reader, gameflow);
                SkipBytes(_reader, GameflowConstants.Unknown3Length, "Unknown3");
                ReadXorKey(_reader, gameflow);
                ReadLanguageId(_reader, gameflow);
                ReadSecretSoundId(_reader, gameflow);
                SkipBytes(_reader, GameflowConstants.Unknown4Length, "Unknown4");

                _useXor = (gameflow.Flags & GameflowFlags.XorEncryption) != 0;
                Log("");
                Log("=== String Array Section ===");

                // String arrays
                gameflow.LevelStrings = ReadTpcStringArray(gameflow.NumLevels, "LevelStrings", gameflow.XorKey);
                gameflow.ChapterScreenStrings = ReadTpcStringArray(gameflow.NumChapterScreens, "ChapterScreenStrings", gameflow.XorKey);
                gameflow.TitleStrings = ReadTpcStringArray(gameflow.NumTitles, "TitleStrings", gameflow.XorKey);
                gameflow.FmvStrings = ReadTpcStringArray(gameflow.NumFmvs, "FmvStrings", gameflow.XorKey);
                gameflow.LevelPathStrings = ReadTpcStringArray(gameflow.NumLevels, "LevelPathStrings", gameflow.XorKey);
                gameflow.CutscenePathStrings = ReadTpcStringArray(gameflow.NumCutscenes, "CutscenePathStrings", gameflow.XorKey);

                // Sequence data section
                Log("");
                Log("=== Sequence Section ===");
                try { ReadSequenceOffsets(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading sequence offsets: {ex.Message}"); }

                try { ReadSequenceNumBytes(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading sequence num bytes: {ex.Message}"); }

                try { ReadSequences(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading sequences: {ex.Message}"); }

                // Demo level IDs
                Log("");
                Log("=== Demo Section ===");
                try { ReadDemoLevelIds(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading demo level IDs: {ex.Message}"); }

                // Additional string arrays (GameStrings, PcStrings, PuzzleStrings, PickupStrings, KeyStrings)
                Log("");
                Log("=== Additional String Arrays ===");
                try { ReadNumGameStrings(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading num game strings: {ex.Message}"); }

                try { gameflow.GameStrings = ReadTpcStringArray(gameflow.NumGameStrings, "GameStrings", gameflow.XorKey); }
                catch (Exception ex) { Log($"⚠ Error reading game strings: {ex.Message}"); }

                try { gameflow.PcStrings = ReadTpcStringArray(GameflowConstants.NumPcStrings, "PcStrings", gameflow.XorKey); }
                catch (Exception ex) { Log($"⚠ Error reading PC strings: {ex.Message}"); }

                // Multi-dimensional arrays
                try { ReadPuzzleStrings(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading puzzle strings: {ex.Message}"); }

                try { ReadPickupStrings(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading pickup strings: {ex.Message}"); }

                try { ReadKeyStrings(_reader, gameflow); }
                catch (Exception ex) { Log($"⚠ Error reading key strings: {ex.Message}"); }

                Log("");
                Log("=== Parse Complete ===");
                LogSummary(gameflow);

                return gameflow;
            }
            catch (Exception ex)
            {
                Log($"⚠ FATAL: {ex.Message}");
                Log($"Stack: {ex.StackTrace}");
                throw;
            }
            finally
            {
                _reader?.Dispose();
                _stream?.Dispose();
            }
        }

        #region Read Methods (Header & Config)

        private void ReadVersion(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.Version = reader.ReadUInt32();
            Log($"Version: 0x{gameflow.Version:X8}");
        }

        private void ReadDescription(BinaryReader reader, Gameflow gameflow)
        {
            byte[] rawBytes = reader.ReadBytes(GameflowConstants.DescriptionLength);
            gameflow.Description = Encoding.ASCII.GetString(rawBytes).TrimEnd('\0');
            Log($"Description: {gameflow.Description.Substring(0, Math.Min(100, gameflow.Description.Length))}...");
        }

        private void ReadGameflowSize(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.GameflowSize = reader.ReadUInt16();
            Log($"GameflowSize: {gameflow.GameflowSize}");
            if (gameflow.GameflowSize != GameflowConstants.ExpectedGameflowSize)
            {
                Log($"  ⚠ Expected {GameflowConstants.ExpectedGameflowSize}, got {gameflow.GameflowSize}");
            }
        }

        private void ReadFirstOption(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.FirstOption = reader.ReadInt32();
        }

        private void ReadTitleReplace(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.TitleReplace = reader.ReadInt32();
        }

        private void ReadOnDeathDemoMode(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.OnDeathDemoMode = reader.ReadInt32();
        }

        private void ReadOnDeathInGame(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.OnDeathInGame = reader.ReadInt32();
        }

        private void ReadDemoTime(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.DemoTime = reader.ReadUInt32();
        }

        private void ReadOnDemoInterrupt(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.OnDemoInterrupt = reader.ReadInt32();
        }

        private void ReadOnDemoEnd(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.OnDemoEnd = reader.ReadInt32();
        }

        #endregion

        #region Read Methods (String Array Counts)

        private void ReadNumLevels(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumLevels = reader.ReadUInt16();
        }

        private void ReadNumChapterScreens(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumChapterScreens = reader.ReadUInt16();
        }

        private void ReadNumTitles(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumTitles = reader.ReadUInt16();
        }

        private void ReadNumFmvs(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumFmvs = reader.ReadUInt16();
        }

        private void ReadNumCutscenes(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumCutscenes = reader.ReadUInt16();
        }

        private void ReadNumDemoLevels(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumDemoLevels = reader.ReadUInt16();
        }

        private void ReadTitleSoundId(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.TitleSoundId = reader.ReadUInt16();
        }

        private void ReadSingleLevel(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.SingleLevel = reader.ReadUInt16();
        }

        #endregion

        #region Read Methods (Flags & Encoding)

        private void ReadFlags(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.Flags = (GameflowFlags)reader.ReadUInt16();
            Log($"Flags: 0x{(ushort)gameflow.Flags:X4}");
        }

        private void ReadXorKey(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.XorKey = reader.ReadByte();
            Log($"XorKey: 0x{gameflow.XorKey:X2}");
        }

        private void ReadLanguageId(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.LanguageId = (GameflowLanguage)reader.ReadByte();
            Log($"LanguageId: {gameflow.LanguageId}");
        }

        private void ReadSecretSoundId(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.SecretSoundId = reader.ReadUInt16();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Reads a TPCStringArray (offset table + totalSize + string data)
        /// </summary>
        private TpcStringArray ReadTpcStringArray(int count, string arrayName, byte xorKey)
        {
            var result = new TpcStringArray(count);

            if (count == 0)
            {
                Log($"[{arrayName}] count=0, skipping");
                return result;
            }

            long startPos = _reader.BaseStream.Position;
            Log($"[{arrayName}] Reading {count} strings at 0x{startPos:X}");

            // Read offset table
            for (int i = 0; i < count; i++)
                result.Offsets[i] = _reader.ReadUInt16();

            Log($"  Offsets sample: {string.Join(",", result.Offsets.Take(Math.Min(5, count)))}...");

            // Read total size
            result.TotalSize = _reader.ReadUInt16();
            Log($"  TotalSize: {result.TotalSize} bytes");

            // Sanity checks
            if (count > 0 && result.Offsets.All(o => o >= GameflowConstants.MaxOffsetThreshold) && result.TotalSize < GameflowConstants.MinTotalSizeForLargeOffsets)
            {
                Log($"  ⚠ Impossible offsets (all >= {GameflowConstants.MaxOffsetThreshold}). Skipping corrupted array.");
                _reader.ReadBytes(result.TotalSize);
                return result;
            }

            if (result.TotalSize == 0)
            {
                Log($"  Empty array.");
                return result;
            }

            // Check file bounds
            long bytesAvailable = _stream.Length - _reader.BaseStream.Position;
            if (result.TotalSize > bytesAvailable)
            {
                Log($"  ⚠ TotalSize {result.TotalSize} exceeds available {bytesAvailable}. File truncated?");
                _reader.ReadBytes((int)Math.Min(bytesAvailable, result.TotalSize));
                return result;
            }

            // Read and decrypt data
            byte[] data = _reader.ReadBytes(result.TotalSize);
            if (_useXor)
            {
                Log($"  Decrypting with XOR 0x{xorKey:X2}...");
                for (int i = 0; i < data.Length; i++)
                    data[i] ^= xorKey;
            }
            result.Data = data;

            Log($"  ✓ {count} strings");
            return result;
        }

        private void SkipBytes(BinaryReader reader, int count, string description = null)
        {
            reader.ReadBytes(count);
            if (!string.IsNullOrEmpty(description))
                Log($"Skipped {count} bytes ({description})");
        }

        #region Read Methods (Sequences, Demo, Additional Arrays)

        private void ReadSequenceOffsets(BinaryReader reader, Gameflow gameflow)
        {
            int numberOfLevels = gameflow.NumLevels + 1;
            gameflow.SequenceOffsets = new ushort[numberOfLevels];
            for (int i = 0; i < numberOfLevels; i++)
                gameflow.SequenceOffsets[i] = reader.ReadUInt16();
            Log($"SequenceOffsets: {numberOfLevels} entries read");
        }

        private void ReadSequenceNumBytes(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.SequenceNumBytes = reader.ReadUInt16();
            Log($"SequenceNumBytes: {gameflow.SequenceNumBytes}");
        }

        private void ReadSequences(BinaryReader reader, Gameflow gameflow)
        {
            var sequences = new List<Sequence>();
            int bytesRead = 0;
            int numberOfLevels = gameflow.NumLevels + 1;
            int currentLevel = 0;

            while (currentLevel < numberOfLevels && bytesRead < gameflow.SequenceNumBytes)
            {
                ushort opcodeValue = reader.ReadUInt16();
                bytesRead += sizeof(ushort);

                var opcode = (SequenceOpcode)opcodeValue;
                ushort? argument = null;

                if (opcode.RequiresArgument())
                {
                    argument = reader.ReadUInt16();
                    bytesRead += sizeof(ushort);
                }

                sequences.Add(new Sequence { Opcode = opcode, Argument = argument });

                if (opcode == SequenceOpcode.End)
                    currentLevel++;
            }

            if (bytesRead != gameflow.SequenceNumBytes)
            {
                Log($"⚠ Sequence byte mismatch: expected {gameflow.SequenceNumBytes}, read {bytesRead}");
            }

            gameflow.Sequences = sequences.ToArray();
            Log($"Sequences: {sequences.Count} opcodes read across {currentLevel} levels");
        }

        private void ReadDemoLevelIds(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.DemoLevelIds = new ushort[gameflow.NumDemoLevels];
            for (int i = 0; i < gameflow.NumDemoLevels; i++)
                gameflow.DemoLevelIds[i] = reader.ReadUInt16();
            Log($"DemoLevelIds: {gameflow.NumDemoLevels} levels");
        }

        private void ReadNumGameStrings(BinaryReader reader, Gameflow gameflow)
        {
            gameflow.NumGameStrings = reader.ReadUInt16();
            Log($"NumGameStrings: {gameflow.NumGameStrings}");
        }

        private void ReadPuzzleStrings(BinaryReader reader, Gameflow gameflow)
        {
            Log($"PuzzleStrings: reading {GameflowConstants.NumPuzzleItemsPerLevel} types x {gameflow.NumLevels} levels");
            for (int type = 0; type < GameflowConstants.NumPuzzleItemsPerLevel; type++)
            {
                gameflow.PuzzleStrings[type] = ReadTpcStringArray(gameflow.NumLevels, $"PuzzleStrings[{type}]", gameflow.XorKey);
            }
        }

        private void ReadPickupStrings(BinaryReader reader, Gameflow gameflow)
        {
            Log($"PickupStrings: reading {GameflowConstants.NumPickupsPerLevel} types x {gameflow.NumLevels} levels");
            for (int type = 0; type < GameflowConstants.NumPickupsPerLevel; type++)
            {
                gameflow.PickupStrings[type] = ReadTpcStringArray(gameflow.NumLevels, $"PickupStrings[{type}]", gameflow.XorKey);
            }
        }

        private void ReadKeyStrings(BinaryReader reader, Gameflow gameflow)
        {
            Log($"KeyStrings: reading {GameflowConstants.NumKeysPerLevel} types x {gameflow.NumLevels} levels");
            for (int type = 0; type < GameflowConstants.NumKeysPerLevel; type++)
            {
                gameflow.KeyStrings[type] = ReadTpcStringArray(gameflow.NumLevels, $"KeyStrings[{type}]", gameflow.XorKey);
            }
        }

        #endregion

        private void LogSummary(Gameflow gameflow)
        {
            int totalStrings = gameflow.LevelStrings.Count
                + gameflow.ChapterScreenStrings.Count
                + gameflow.TitleStrings.Count
                + gameflow.FmvStrings.Count
                + gameflow.LevelPathStrings.Count
                + gameflow.CutscenePathStrings.Count
                + gameflow.GameStrings.Count
                + gameflow.PcStrings.Count;

            Log($"");
            Log($"=== Parse Summary ===");
            Log($"String Arrays:");
            Log($"  Levels: {gameflow.NumLevels} ({gameflow.LevelStrings.Count} strings)");
            Log($"  Chapters: {gameflow.NumChapterScreens} ({gameflow.ChapterScreenStrings.Count} strings)");
            Log($"  Titles: {gameflow.NumTitles} ({gameflow.TitleStrings.Count} strings)");
            Log($"  FMVs: {gameflow.NumFmvs} ({gameflow.FmvStrings.Count} strings)");
            Log($"  LevelPaths: {gameflow.NumLevels} ({gameflow.LevelPathStrings.Count} strings)");
            Log($"  Cutscenes: {gameflow.NumCutscenes} ({gameflow.CutscenePathStrings.Count} strings)");
            Log($"  GameStrings: {gameflow.NumGameStrings} ({gameflow.GameStrings.Count} strings)");
            Log($"  PcStrings: {GameflowConstants.NumPcStrings} ({gameflow.PcStrings.Count} strings)");
            Log($"  Total Strings: {totalStrings}");

            if (gameflow.Sequences != null)
            {
                Log($"Sequence Data:");
                Log($"  Sequences: {gameflow.Sequences.Length} opcodes");
                Log($"  SequenceNumBytes: {gameflow.SequenceNumBytes}");
            }

            if (gameflow.DemoLevelIds != null)
            {
                Log($"Demo Data:");
                Log($"  DemoLevelIds: {gameflow.DemoLevelIds.Length} levels");
            }

            int puzzleTotal = gameflow.PuzzleStrings.Sum(p => p.Count);
            int pickupTotal = gameflow.PickupStrings.Sum(p => p.Count);
            int keyTotal = gameflow.KeyStrings.Sum(p => p.Count);
            Log($"Item Strings:");
            Log($"  PuzzleStrings: {puzzleTotal} total ({GameflowConstants.NumPuzzleItemsPerLevel} types)");
            Log($"  PickupStrings: {pickupTotal} total ({GameflowConstants.NumPickupsPerLevel} types)");
            Log($"  KeyStrings: {keyTotal} total ({GameflowConstants.NumKeysPerLevel} types)");
        }

        private void Log(string message)
        {
            _logger?.Invoke(message);
        }

        #endregion
    }
}
