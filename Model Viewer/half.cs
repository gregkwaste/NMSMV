using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



public static class Half
{
    public static float decompress(uint float16)
    {
        UInt32 t1 = float16 & 0x7fff; //Non-sign bits
        UInt32 t2 = float16 & 0x8000; //Sign bit
        UInt32 t3 = float16 & 0x7c00; //Exponent


        t1 <<= 13;                              // Align mantissa on MSB
        t2 <<= 16;                              // Shift sign bit into position

        t1 += 0x38000000;                       // Adjust bias

        t1 = (t3 == 0 ? 0 : t1);                // Denormals-as-zero

        t1 |= t2;                               // Re-insert sign bit

        return BitConverter.ToSingle(BitConverter.GetBytes(t1), 0);
    }
}

