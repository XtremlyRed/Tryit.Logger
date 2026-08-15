using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tryit.Logger;

internal static class Extensions
{
    public static double CoerceAtLeast(this double inputValue, double minValue)
    {
        return inputValue < minValue ? minValue : inputValue;
    }
}
