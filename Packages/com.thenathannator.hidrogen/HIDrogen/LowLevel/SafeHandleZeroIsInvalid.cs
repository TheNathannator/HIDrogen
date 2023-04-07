using System;
using System.Runtime.InteropServices;

namespace HIDrogen.LowLevel
{
    /// <summary>
    /// A safe handle where a handle value of 0 is invalid.
    /// </summary>
    internal abstract class SafeHandleZeroIsInvalid : SafeHandle
    {
        protected SafeHandleZeroIsInvalid()
            : base(IntPtr.Zero, true)
        {
        }

        protected SafeHandleZeroIsInvalid(bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;
    }
}