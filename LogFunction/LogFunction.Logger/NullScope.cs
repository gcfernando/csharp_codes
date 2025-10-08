using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogFunction.Logger;
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    private NullScope()
    { }

    public void Dispose()
    { }
}