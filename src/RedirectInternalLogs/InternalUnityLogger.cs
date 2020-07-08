using System;

namespace RedirectInternalLogs
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

        internal static void OnUnityLog(InternalLogLevel logLevel, string message, IntPtr parts)
        {
            OnUnityInternalLog?.Invoke(null, new UnityLogEventArgs
            {
                LogLevel = logLevel,
                Message = LibcHelper.Format(message, parts)
            });
        }
    }
}