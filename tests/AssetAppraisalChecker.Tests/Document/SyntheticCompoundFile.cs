using System;
using System.IO;
using System.Text;

namespace AssetAppraisalChecker.Tests.Document
{
    internal static class SyntheticCompoundFile
    {
        private const int SectorSize = 512;
        private const uint FreeSector = 0xFFFFFFFF;
        private const uint EndOfChain = 0xFFFFFFFE;
        private const uint FatSector = 0xFFFFFFFD;
        private const uint NoStream = 0xFFFFFFFF;

        public static byte[] Create(params string[] streamNames)
        {
            if (streamNames == null)
            {
                throw new ArgumentNullException(nameof(streamNames));
            }

            if (streamNames.Length > 3)
            {
                throw new ArgumentException("单个测试目录扇区最多容纳根项和三个流。", nameof(streamNames));
            }

            var content = new byte[SectorSize * 3];
            WriteHeader(content);
            WriteFatSector(content, SectorSize);
            WriteDirectoryEntry(content, SectorSize * 2, "Root Entry", 5);
            for (int index = 0; index < streamNames.Length; index++)
            {
                WriteDirectoryEntry(
                    content,
                    (SectorSize * 2) + ((index + 1) * 128),
                    streamNames[index],
                    2);
            }

            return content;
        }

        private static void WriteHeader(byte[] content)
        {
            byte[] signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            Buffer.BlockCopy(signature, 0, content, 0, signature.Length);
            WriteUInt16(content, 24, 0x003E);
            WriteUInt16(content, 26, 3);
            WriteUInt16(content, 28, 0xFFFE);
            WriteUInt16(content, 30, 9);
            WriteUInt16(content, 32, 6);
            WriteUInt32(content, 40, 0);
            WriteUInt32(content, 44, 1);
            WriteUInt32(content, 48, 1);
            WriteUInt32(content, 56, 4096);
            WriteUInt32(content, 60, EndOfChain);
            WriteUInt32(content, 64, 0);
            WriteUInt32(content, 68, EndOfChain);
            WriteUInt32(content, 72, 0);

            for (int index = 0; index < 109; index++)
            {
                WriteUInt32(content, 76 + (index * sizeof(uint)), FreeSector);
            }

            WriteUInt32(content, 76, 0);
        }

        private static void WriteFatSector(byte[] content, int offset)
        {
            for (int index = 0; index < SectorSize / sizeof(uint); index++)
            {
                WriteUInt32(content, offset + (index * sizeof(uint)), FreeSector);
            }

            WriteUInt32(content, offset, FatSector);
            WriteUInt32(content, offset + sizeof(uint), EndOfChain);
        }

        private static void WriteDirectoryEntry(
            byte[] content,
            int offset,
            string name,
            byte objectType)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 31)
            {
                throw new ArgumentException("测试目录项名称必须包含 1 到 31 个字符。", nameof(name));
            }

            byte[] nameBytes = Encoding.Unicode.GetBytes(name + '\0');
            Buffer.BlockCopy(nameBytes, 0, content, offset, nameBytes.Length);
            WriteUInt16(content, offset + 64, (ushort)nameBytes.Length);
            content[offset + 66] = objectType;
            content[offset + 67] = 1;
            WriteUInt32(content, offset + 68, NoStream);
            WriteUInt32(content, offset + 72, NoStream);
            WriteUInt32(content, offset + 76, NoStream);
            WriteUInt32(content, offset + 116, EndOfChain);
            WriteUInt32(content, offset + 120, 0);
            WriteUInt32(content, offset + 124, 0);
        }

        private static void WriteUInt16(byte[] content, int offset, ushort value)
        {
            content[offset] = (byte)value;
            content[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] content, int offset, uint value)
        {
            content[offset] = (byte)value;
            content[offset + 1] = (byte)(value >> 8);
            content[offset + 2] = (byte)(value >> 16);
            content[offset + 3] = (byte)(value >> 24);
        }
    }
}
