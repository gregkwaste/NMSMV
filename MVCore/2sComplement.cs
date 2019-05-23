using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

public class _2sComplement
{
    static public int toInt(uint val, int bits)
    {
        int mask = 1 << (bits - 1);
        return (int) (-(val & mask) + (val & ~mask));
    }
}

