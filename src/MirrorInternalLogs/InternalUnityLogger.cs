using System;
using System.IO;
using MirrorInternalLogs.Util;

namespace MirrorInternalLogs
{
    public enum InternalLogLevel
    {
        Error,
        Assert,
        Warning,
        Log,
        Exception,
        Debug
    }

    public class UnityLogEventArgs : EventArgs
    {
        public InternalLogLevel LogLevel { get; internal set; }
        public string Message { get; internal set; }
    }

    public static class InternalUnityLogger
    {
        public static event EventHandler<UnityLogEventArgs> OnUnityInternalLog;

        internal static void OnLogHook(ulong type, IntPtr pattern, IntPtr args)
        {
            OnUnityLog((InternalLogLevel)type, pattern, args);
        }

        internal static void OnLogHook(uint type, IntPtr pattern, IntPtr args)
        {
            OnUnityLog((InternalLogLevel)type, pattern, args);
        }

        internal static void OnLogHook(IntPtr pattern, IntPtr args)
        {
            OnUnityLog(InternalLogLevel.Log, pattern, args);
        }

        internal static void OnUnityLog(InternalLogLevel logLevel, IntPtr message, IntPtr parts)
        {
            try
            {
                OnUnityInternalLog?.Invoke(null, new UnityLogEventArgs
                {
                    LogLevel = logLevel,
                    Message = LibcHelper.Format(message, parts)
                });
            }
            catch (Exception e)
            {
                File.WriteAllText($"unity_logger_err_{DateTime.Now.Ticks}.txt", e.ToString());
            }
        }
    }
}
