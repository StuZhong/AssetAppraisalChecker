using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetAppraisalChecker.Document
{
    internal enum CompoundFileKind
    {
        NotCompoundFile = 0,
        EncryptedOfficePackage,
        LegacyWordBinary,
        Unknown
    }

    /// <summary>
    /// Inspects only the CFB header, allocation tables, and directory entry names.
    /// It never opens or interprets document-content streams.
    /// </summary>
    internal static class CompoundFileFormatDetector
    {
        private const int HeaderLength = 512;
        private const uint FreeSector = 0xFFFFFFFF;
        private const uint EndOfChain = 0xFFFFFFFE;
        private const uint FatSector = 0xFFFFFFFD;
        private const int HeaderDifatEntryCount = 109;
        private const int DirectoryEntryLength = 128;

        private static readonly byte[] CompoundFileSignature =
        {
            0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1
        };

        public static CompoundFileKind Detect(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("复合文件检测需要可读取且可定位的流。", nameof(stream));
            }

            long originalPosition = stream.Position;
            try
            {
                if (!HasCompoundFileSignature(stream))
                {
                    return CompoundFileKind.NotCompoundFile;
                }

                try
                {
                    HashSet<string> entryNames = ReadDirectoryEntryNames(stream);
                    if (entryNames.Contains("EncryptedPackage") &&
                        entryNames.Contains("EncryptionInfo"))
                    {
                        return CompoundFileKind.EncryptedOfficePackage;
                    }

                    if (entryNames.Contains("WordDocument") &&
                        (entryNames.Contains("0Table") || entryNames.Contains("1Table")))
                    {
                        return CompoundFileKind.LegacyWordBinary;
                    }

                    return CompoundFileKind.Unknown;
                }
                catch (InvalidDataException)
                {
                    return CompoundFileKind.Unknown;
                }
                catch (OverflowException)
                {
                    return CompoundFileKind.Unknown;
                }
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        private static bool HasCompoundFileSignature(Stream stream)
        {
            if (stream.Length < CompoundFileSignature.Length)
            {
                return false;
            }

            var signature = new byte[CompoundFileSignature.Length];
            ReadExactly(stream, 0, signature, 0, signature.Length);
            for (int index = 0; index < signature.Length; index++)
            {
                if (signature[index] != CompoundFileSignature[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static HashSet<string> ReadDirectoryEntryNames(Stream stream)
        {
            if (stream.Length < HeaderLength)
            {
                throw InvalidCompoundFile();
            }

            var header = new byte[HeaderLength];
            ReadExactly(stream, 0, header, 0, header.Length);

            ushort majorVersion = ReadUInt16(header, 26);
            ushort byteOrder = ReadUInt16(header, 28);
            ushort sectorShift = ReadUInt16(header, 30);
            int expectedSectorShift = majorVersion == 3 ? 9 : majorVersion == 4 ? 12 : -1;
            if (byteOrder != 0xFFFE || sectorShift != expectedSectorShift)
            {
                throw InvalidCompoundFile();
            }

            int sectorSize = 1 << sectorShift;
            if (stream.Length % sectorSize != 0)
            {
                throw InvalidCompoundFile();
            }

            long physicalSectorCountLong = (stream.Length / sectorSize) - 1;
            if (physicalSectorCountLong <= 0 || physicalSectorCountLong > int.MaxValue)
            {
                throw InvalidCompoundFile();
            }

            int physicalSectorCount = (int)physicalSectorCountLong;
            uint fatSectorCount = ReadUInt32(header, 44);
            uint firstDirectorySector = ReadUInt32(header, 48);
            uint firstDifatSector = ReadUInt32(header, 68);
            uint difatSectorCount = ReadUInt32(header, 72);
            if (fatSectorCount == 0 ||
                fatSectorCount > physicalSectorCount ||
                difatSectorCount > physicalSectorCount)
            {
                throw InvalidCompoundFile();
            }

            List<uint> fatSectorIds = ReadFatSectorIds(
                stream,
                header,
                sectorSize,
                physicalSectorCount,
                fatSectorCount,
                firstDifatSector,
                difatSectorCount);
            List<uint> fat = ReadFat(stream, sectorSize, physicalSectorCount, fatSectorIds);
            return ReadDirectoryChain(
                stream,
                sectorSize,
                physicalSectorCount,
                firstDirectorySector,
                fat);
        }

        private static List<uint> ReadFatSectorIds(
            Stream stream,
            byte[] header,
            int sectorSize,
            int physicalSectorCount,
            uint fatSectorCount,
            uint firstDifatSector,
            uint difatSectorCount)
        {
            var result = new List<uint>();
            var uniqueFatSectors = new HashSet<uint>();
            for (int index = 0; index < HeaderDifatEntryCount; index++)
            {
                uint sectorId = ReadUInt32(header, 76 + (index * sizeof(uint)));
                AddFatSectorId(
                    result,
                    uniqueFatSectors,
                    sectorId,
                    fatSectorCount,
                    physicalSectorCount);
            }

            uint difatSector = firstDifatSector;
            var visitedDifatSectors = new HashSet<uint>();
            int entriesPerDifatSector = (sectorSize / sizeof(uint)) - 1;
            for (uint index = 0; index < difatSectorCount && result.Count < fatSectorCount; index++)
            {
                ValidateRegularSector(difatSector, physicalSectorCount);
                if (!visitedDifatSectors.Add(difatSector))
                {
                    throw InvalidCompoundFile();
                }

                byte[] sector = ReadSector(stream, difatSector, sectorSize, physicalSectorCount);
                for (int entryIndex = 0;
                     entryIndex < entriesPerDifatSector && result.Count < fatSectorCount;
                     entryIndex++)
                {
                    AddFatSectorId(
                        result,
                        uniqueFatSectors,
                        ReadUInt32(sector, entryIndex * sizeof(uint)),
                        fatSectorCount,
                        physicalSectorCount);
                }

                difatSector = ReadUInt32(sector, sector.Length - sizeof(uint));
            }

            if (result.Count != fatSectorCount)
            {
                throw InvalidCompoundFile();
            }

            return result;
        }

        private static void AddFatSectorId(
            ICollection<uint> result,
            ISet<uint> uniqueFatSectors,
            uint sectorId,
            uint fatSectorCount,
            int physicalSectorCount)
        {
            if (sectorId == FreeSector || result.Count >= fatSectorCount)
            {
                return;
            }

            ValidateRegularSector(sectorId, physicalSectorCount);
            if (!uniqueFatSectors.Add(sectorId))
            {
                throw InvalidCompoundFile();
            }

            result.Add(sectorId);
        }

        private static List<uint> ReadFat(
            Stream stream,
            int sectorSize,
            int physicalSectorCount,
            IEnumerable<uint> fatSectorIds)
        {
            int entriesPerSector = sectorSize / sizeof(uint);
            var fat = new List<uint>();
            foreach (uint sectorId in fatSectorIds)
            {
                byte[] sector = ReadSector(stream, sectorId, sectorSize, physicalSectorCount);
                for (int index = 0; index < entriesPerSector; index++)
                {
                    fat.Add(ReadUInt32(sector, index * sizeof(uint)));
                }
            }

            return fat;
        }

        private static HashSet<string> ReadDirectoryChain(
            Stream stream,
            int sectorSize,
            int physicalSectorCount,
            uint firstDirectorySector,
            IReadOnlyList<uint> fat)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var visitedDirectorySectors = new HashSet<uint>();
            uint directorySector = firstDirectorySector;

            while (directorySector != EndOfChain)
            {
                ValidateRegularSector(directorySector, physicalSectorCount);
                if (directorySector >= fat.Count ||
                    !visitedDirectorySectors.Add(directorySector) ||
                    visitedDirectorySectors.Count > physicalSectorCount)
                {
                    throw InvalidCompoundFile();
                }

                byte[] sector = ReadSector(stream, directorySector, sectorSize, physicalSectorCount);
                for (int offset = 0; offset + DirectoryEntryLength <= sector.Length; offset += DirectoryEntryLength)
                {
                    byte objectType = sector[offset + 66];
                    ushort nameLength = ReadUInt16(sector, offset + 64);
                    if (objectType == 0)
                    {
                        continue;
                    }

                    if (nameLength < 2 || nameLength > 64 || nameLength % 2 != 0)
                    {
                        throw InvalidCompoundFile();
                    }

                    if (objectType != 1 && objectType != 2 && objectType != 5)
                    {
                        throw InvalidCompoundFile();
                    }

                    if (objectType == 2)
                    {
                        names.Add(Encoding.Unicode.GetString(sector, offset, nameLength - 2));
                    }
                }

                directorySector = fat[(int)directorySector];
                if (directorySector == FreeSector || directorySector == FatSector)
                {
                    throw InvalidCompoundFile();
                }
            }

            return names;
        }

        private static byte[] ReadSector(
            Stream stream,
            uint sectorId,
            int sectorSize,
            int physicalSectorCount)
        {
            ValidateRegularSector(sectorId, physicalSectorCount);
            var buffer = new byte[sectorSize];
            long offset = checked(((long)sectorId + 1) * sectorSize);
            ReadExactly(stream, offset, buffer, 0, buffer.Length);
            return buffer;
        }

        private static void ValidateRegularSector(uint sectorId, int physicalSectorCount)
        {
            if (sectorId >= physicalSectorCount)
            {
                throw InvalidCompoundFile();
            }
        }

        private static void ReadExactly(
            Stream stream,
            long position,
            byte[] buffer,
            int offset,
            int count)
        {
            stream.Position = position;
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    throw InvalidCompoundFile();
                }

                totalRead += read;
            }
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] |
                          (buffer[offset + 1] << 8) |
                          (buffer[offset + 2] << 16) |
                          (buffer[offset + 3] << 24));
        }

        private static InvalidDataException InvalidCompoundFile()
        {
            return new InvalidDataException("复合文件容器结构无效。");
        }
    }
}
