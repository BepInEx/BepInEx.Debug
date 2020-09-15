using System;

namespace MirrorInternalLogs.Platforms
{
    internal interface IPlatformPatcher
    {
        void Patch(IntPtr unityModule, int moduleSize);
    }
}