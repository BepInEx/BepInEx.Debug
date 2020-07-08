using System;

namespace RedirectInternalLogs.Platforms
{
    internal interface IPlatformPatcher
    {
        void Patch(IntPtr unityModule, int moduleSize);
    }
}