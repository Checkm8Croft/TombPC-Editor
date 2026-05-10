namespace TombPC_Editor
{
    /// <summary>
    /// Central repository for TOMBPC.DAT format constants and magic numbers.
    /// Based on TRosettaStone specification and GameflowReader reference implementation.
    /// </summary>
    public static class GameflowConstants
    {
        // Header structure sizes
        public const int VersionSize = 4;                   // uint32
        public const int DescriptionLength = 256;          // ASCII string
        public const int GameflowSizeFieldSize = 2;        // uint16

        // First section of gameflow (after version + description + gameflowSize)
        public const int FirstOptionSize = 4;              // int32
        public const int TitleReplaceSize = 4;             // int32
        public const int OnDeathDemoModeSize = 4;          // int32
        public const int OnDeathInGameSize = 4;            // int32
        public const int DemoTimeSize = 4;                 // uint32
        public const int OnDemoInterruptSize = 4;          // int32
        public const int OnDemoEndSize = 4;                // int32

        public const int Unknown1Length = 36;              // Unknown padding/fields

        // String array counts
        public const int NumLevelsSize = 2;                // uint16
        public const int NumChapterScreensSize = 2;        // uint16
        public const int NumTitlesSize = 2;                // uint16
        public const int NumFmvsSize = 2;                  // uint16
        public const int NumCutscenesSize = 2;             // uint16
        public const int NumDemoLevelsSize = 2;            // uint16
        public const int TitleSoundIdSize = 2;             // uint16
        public const int SingleLevelSize = 2;              // uint16

        public const int Unknown2Length = 32;              // Unknown padding/fields

        // Flags and config
        public const int FlagsSize = 2;                    // uint16
        public const int Unknown3Length = 6;               // Unknown padding
        public const int XorKeySize = 1;                   // byte
        public const int LanguageIdSize = 1;               // byte
        public const int SecretSoundIdSize = 2;            // uint16
        public const int Unknown4Length = 4;               // Unknown padding

        // Flags bitwise values
        public const ushort FlagXorEncryption = 0x0100;    // bit 8: enable XOR encryption

        // Expected values (for validation)
        public const ushort ExpectedGameflowSize = 512;

        // String arrays (Phase 1-3)
        public const int NumPcStrings = 41;                // PC-specific strings (uint16_t count + array)

        // Phase 4: Item strings (Puzzle, Pickup, Key)
        // Structure: TPCStringArray[type] where each contains NumLevels strings
        public const int NumPuzzleItemsPerLevel = 4;       // Puzzle types (4 per level set)
        public const int NumPickupsPerLevel = 2;           // Pickup types (2 per level set)
        public const int NumKeysPerLevel = 4;              // Key types (4 per level set)

        // Phase 4: Additional string array counts
        public const int NumGameStringsSize = 2;           // uint16 - number of game-specific strings
        public const int NumGameStringsCountSize = 2;      // uint16 - count field before GameStrings array
        public const int NumPcStringsCountSize = 0;        // No count field; always 41 strings for PC

        // TpcStringArray format
        public const int OffsetSize = 2;                   // uint16 per offset
        public const int TotalSizeFieldSize = 2;           // uint16 for total data size

        // XOR encryption
        public const byte DefaultXorKey = 0xA6;            // Default XOR key for encrypted strings

        // Sanity check thresholds
        public const int MaxOffsetThreshold = 1000;        // Offsets >= this are suspicious if totalSize < 1000
        public const int MinTotalSizeForLargeOffsets = 1000;

        // Sequence opcodes
        public const ushort SequenceOpcodeEnd = 0x0002;    // Marks end of sequence level

        // Sequence data section
        public const int SequenceOffsetSize = 2;           // uint16 per offset
        public const int SequenceNumBytesSize = 2;         // uint16 for total sequence bytes
        public const int SequenceOpcodeSize = 2;           // uint16 per opcode
        public const int SequenceArgumentSize = 2;         // uint16 per argument
    }
}
