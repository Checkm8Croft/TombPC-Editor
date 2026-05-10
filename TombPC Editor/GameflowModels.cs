namespace TombPC_Editor
{
    /// <summary>
    /// Enumeration of gameflow flags (bitwise).
    /// </summary>
    public enum GameflowFlags : ushort
    {
        XorEncryption = GameflowConstants.FlagXorEncryption,
    }

    /// <summary>
    /// Enumeration of language IDs used in TOMBPC.DAT
    /// </summary>
    public enum GameflowLanguage : byte
    {
        English = 0,
        French = 1,
        German = 2,
        Spanish = 3,
        Italian = 4,
        Russian = 5,
        Polish = 6,
        Czech = 7,
        Hungarian = 8,
    }

    /// <summary>
    /// Enumeration of gameflow sequence opcodes.
    /// These are 16-bit instructions that control gameflow logic (cutscenes, level progression, etc.)
    /// </summary>
    public enum SequenceOpcode : ushort
    {
        // Opcode values (reference from TRosettaStone/GameflowReader)
        End = 0x0002,           // End of sequence for this level
        // Additional opcodes can be added as needed
    }

    /// <summary>
    /// Extension methods for SequenceOpcode
    /// </summary>
    public static class SequenceOpcodeExtensions
    {
        /// <summary>
        /// Determines if this opcode requires an argument (16-bit value following the opcode)
        /// </summary>
        public static bool RequiresArgument(this SequenceOpcode opcode)
        {
            // Most opcodes require an argument; only a few (like End) don't
            return opcode != SequenceOpcode.End;
        }
    }

    /// <summary>
    /// Represents a single sequence instruction (opcode + optional argument)
    /// </summary>
    public class Sequence
    {
        public SequenceOpcode Opcode { get; set; }
        public ushort? Argument { get; set; }
    }

    /// <summary>
    /// Represents a TPCStringArray structure from TOMBPC.DAT
    /// Format: [offset table (uint16 per string)] [totalSize (uint16)] [string data (bytes)]
    /// </summary>
    public class TpcStringArray
    {
        /// <summary>
        /// Number of strings in this array
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Offset table: position of each string within the data block
        /// </summary>
        public ushort[] Offsets { get; }

        /// <summary>
        /// Total size of the string data block (in bytes)
        /// </summary>
        public ushort TotalSize { get; set; }

        /// <summary>
        /// Raw byte data for all strings in this array
        /// </summary>
        public byte[]? Data { get; set; }

        public TpcStringArray(int count)
        {
            Count = count;
            Offsets = new ushort[count];
            Data = null;
            TotalSize = 0;
        }
    }

    /// <summary>
    /// Main gameflow data structure containing all TOMBPC.DAT information
    /// </summary>
    public class Gameflow
    {
        // Header section
        public uint Version { get; set; }
        public string Description { get; set; } = string.Empty;
        public ushort GameflowSize { get; set; }

        // Configuration section
        public int FirstOption { get; set; }
        public int TitleReplace { get; set; }
        public int OnDeathDemoMode { get; set; }
        public int OnDeathInGame { get; set; }
        public uint DemoTime { get; set; }
        public int OnDemoInterrupt { get; set; }
        public int OnDemoEnd { get; set; }

        // String array counts
        public ushort NumLevels { get; set; }
        public ushort NumChapterScreens { get; set; }
        public ushort NumTitles { get; set; }
        public ushort NumFmvs { get; set; }
        public ushort NumCutscenes { get; set; }
        public ushort NumDemoLevels { get; set; }
        public ushort TitleSoundId { get; set; }
        public ushort SingleLevel { get; set; }

        // Flags and encoding
        public GameflowFlags Flags { get; set; }
        public byte XorKey { get; set; }
        public GameflowLanguage LanguageId { get; set; }
        public ushort SecretSoundId { get; set; }

        // String arrays (the main content)
        public TpcStringArray LevelStrings { get; set; } = new(0);
        public TpcStringArray ChapterScreenStrings { get; set; } = new(0);
        public TpcStringArray TitleStrings { get; set; } = new(0);
        public TpcStringArray FmvStrings { get; set; } = new(0);
        public TpcStringArray LevelPathStrings { get; set; } = new(0);
        public TpcStringArray CutscenePathStrings { get; set; } = new(0);

        // Sequence data (gameflow logic)
        public ushort[]? SequenceOffsets { get; set; }
        public ushort SequenceNumBytes { get; set; }
        public Sequence[]? Sequences { get; set; }

        // Additional arrays
        public ushort[]? DemoLevelIds { get; set; }
        public ushort NumGameStrings { get; set; }
        public TpcStringArray GameStrings { get; set; } = new(0);
        public TpcStringArray PcStrings { get; set; } = new(0);

        // Multi-dimensional arrays (indexed by type, then by level)
        public TpcStringArray[] PuzzleStrings { get; }
        public TpcStringArray[] PickupStrings { get; }
        public TpcStringArray[] KeyStrings { get; }

        public Gameflow()
        {
            // Initialize multi-dimensional arrays
            PuzzleStrings = new TpcStringArray[GameflowConstants.NumPuzzleItemsPerLevel];
            PickupStrings = new TpcStringArray[GameflowConstants.NumPickupsPerLevel];
            KeyStrings = new TpcStringArray[GameflowConstants.NumKeysPerLevel];

            // Initialize with empty arrays
            for (int i = 0; i < PuzzleStrings.Length; i++)
                PuzzleStrings[i] = new(0);
            for (int i = 0; i < PickupStrings.Length; i++)
                PickupStrings[i] = new(0);
            for (int i = 0; i < KeyStrings.Length; i++)
                KeyStrings[i] = new(0);
        }
    }

    /// <summary>
    /// Internal helper class to represent a string entry in the editor UI
    /// </summary>
    internal class StringEntry
    {
        public long Offset { get; set; }
        public int Length { get; set; }  // including terminating 0
        public string Text { get; set; } = string.Empty;
    }
}
