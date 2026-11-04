using System;

namespace HIDrogen
{
    internal interface ICustomInputService : IDisposable
    {
        void Start();
        void Stop();
    }
}