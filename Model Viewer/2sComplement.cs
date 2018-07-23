using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

class _2sComplement
{
    static public int toInt(uint val, int bits)
    {
        int output;

        //Check MSB
        int one = 1;
        int mask = one << (bits - 1);
        int full_mask = 1;

        //Init Full mask
        for (int i=1; i <= bits; i++) full_mask = full_mask | (one << (i - 1));
        
        uint flag = val & (uint) mask;

        //Convert number
        if (val > 512)
        {
            Debug.WriteLine("PREPARE FOR MALAKIA");

        }

        if (flag !=0 )
        {
            output = (int)(~val + 1);
            output &= full_mask;
            output = -output;
        }
        else
        {
            output = (int) val;
        }

        if (output > 512)
        {
            Debug.WriteLine("MALAKIES");
        }

        return output;
    }
}

