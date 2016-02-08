using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shaman.Runtime.Serialization
{
    internal static class SerializationExtensions
    {



        [AllowNumericLiterals]
        public static void Write7BitEncodedInt(this BinaryWriter bw, int value)
        {
            uint num;
            for (num = (uint)value; num >= 128u; num >>= 7)
            {
                bw.Write((byte)(num | 128u));
            }
            bw.Write((byte)num);
        }
        [AllowNumericLiterals]

        public static void Write7BitEncodedLong(this BinaryWriter bw, long value)
        {
            ulong num;
            for (num = (ulong)value; num >= 128u; num >>= 7)
            {
                bw.Write((byte)(num | 128u));
            }
            bw.Write((byte)num);
        }
        [AllowNumericLiterals]
        public static int Read7BitEncodedInt(this BinaryReader br)
        {
            int num = 0;
            int num2 = 0;
            while (num2 != 35)
            {
                byte b = br.ReadByte();
                num |= (int)(b & 127) << num2;
                num2 += 7;
                if ((b & 128) == 0)
                {
                    return num;
                }
            }
            throw Sanity.ShouldntHaveHappened();
        }

        [AllowNumericLiterals]
        public static long Read7BitEncodedLong(this BinaryReader br)
        {
            long num = 0;
            int num2 = 0;
            while (true)
            {
                byte b = br.ReadByte();
                num |= (long)(b & 127) << num2;
                num2 += 7;
                if ((b & 128) == 0)
                {
                    return num;
                }
            }
            throw Sanity.ShouldntHaveHappened();
        }

    }
}
