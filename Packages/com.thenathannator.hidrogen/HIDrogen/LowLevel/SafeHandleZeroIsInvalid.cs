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
            SetHandle(IntPtr.Zero);
        }

        protected SafeHandleZeroIsInvalid(IntPtr handle, bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
            SetHandle(handle);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;
    }
}