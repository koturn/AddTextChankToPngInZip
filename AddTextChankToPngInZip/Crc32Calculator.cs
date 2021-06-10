using System;


namespace AddTextChankToPngInZip
{
    public class Crc32Calculator
    {
        private static uint[] _table;


        public static uint Compute(byte[] buf)
        {
            return Compute(buf.AsSpan());
        }

        public static uint Compute(byte[] buf, int offset, int count)
        {
            return Compute(buf.AsSpan(offset, count));
        }

        public static uint Compute(Span<byte> buf)
        {
            return Finalize(Update(buf));
        }


        public static uint Update(byte[] buf, uint crc = 0xffffffff)
        {
            return Update(buf.AsSpan(), crc);
        }

        public static uint Update(byte[] buf, int offset, int count, uint crc = 0xffffffff)
        {
            return Update(buf.AsSpan(offset, count), crc);
        }

        public static uint Update(Span<byte> buf, uint crc = 0xffffffff)
        {
            var crcTable = GetTable();

            var c = crc;
            foreach (var x in buf)
            {
                c = crcTable[(c ^ x) & 0xff] ^ (c >> 8);
            }

            return c;
        }


        public static uint Update(byte x, uint crc = 0xffffffff)
        {
            return GetTable()[(crc ^ (x & 0xff)) & 0xff] ^ (crc >> 8);
        }


        public static uint Finalize(uint crc)
        {
            return crc ^ 0xffffffff;
        }


        private static uint[] GetTable()
        {
            return _table ??= GenerateTable();
        }

        private static uint[] GenerateTable()
        {
            var crcTable = new uint[256];

            for (int n = 0; n < crcTable.Length; n++)
            {
                var c = (uint)n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? (0xedb88320 ^ (c >> 1)) : (c >> 1);
                }
                crcTable[n] = c;
            }

            return crcTable;
        }
    }
}
