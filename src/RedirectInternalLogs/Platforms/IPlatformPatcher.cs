using System;

namespace RedirectInternalLogs
{
    public interface IPlatformPatcher
    {
        void Patch(IntPtr unityModule, int moduleSize);
    }
}