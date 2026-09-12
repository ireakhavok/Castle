using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    internal static unsafe class ComVtable
    {
        public static nint Slot(nint obj, int index)
        {
            nint* vtbl = *(nint**)obj;
            return vtbl[index];
        }

        public static int Call0(nint obj, int slot)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, int>)Slot(obj, slot);
            return fn(obj);
        }

        public static int Call2i(nint obj, int slot, int a, int b)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, int, int, int>)Slot(obj, slot);
            return fn(obj, a, b);
        }

        public static int Call3(nint obj, int slot, nint a, nint b, nint c)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, nint, nint, nint, int>)Slot(obj, slot);
            return fn(obj, a, b, c);
        }

        public static int Call4(nint obj, int slot, uint a, nint b, nint c, nint d)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint, int>)Slot(obj, slot);
            return fn(obj, a, b, c, d);
        }

        public static uint Release(nint obj)
        {
            if (obj == nint.Zero) return 0;
            var fn = (delegate* unmanaged[Stdcall]<nint, uint>)Slot(obj, 2);
            return fn(obj);
        }

        public static void ThrowIfFailed(int hr, string what)
        {
            if (hr < 0)
                throw new InvalidOperationException($"{what} failed HRESULT=0x{hr:X8}");
        }
    }
}
