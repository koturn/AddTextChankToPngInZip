namespace AddTextChankToPngInZip
{
    public class Crc32Calculator
    {
        private static uint[] _table;


        public static uint Compute(byte[] buf)
        {
            return Compute(buf, 0, buf.Length);
        }

        public static uint Compute(byte[] buf, int offset, int count)
        {
            return Finalize(Update(buf, offset, count));
        }


        public static uint Update(byte[] buf, uint crc = 0xffffffff)
        {
            return Update(buf, 0, buf.Length, crc);
        }

        public static uint Update(byte[] buf, int offset, int count, uint crc = 0xffffffff)
        {
            var crcTable = GetTable();

            var c = crc;
            var n = offset + count;
            for (int i = offset; i < n; i++)
            {
                c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >> 8);
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
