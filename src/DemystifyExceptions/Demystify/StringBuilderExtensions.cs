using System;
using DemystifyExceptions.Demystify.Internal;

namespace DemystifyExceptions.Demystify
{
    internal static class StringBuilderExtensions
    {
        internal static StringBuilder AppendDemystified(this StringBuilder builder, Exception exception)
        {
            try
            {
                var stackTrace = new EnhancedStackTrace(exception);

                builder.Append(exception.GetType());
                if (!string.IsNullOrEmpty(exception.Message))
                    builder.Append(": ").Append(exception.Message);

                builder.Append(Environment.NewLine);

                if (stackTrace.FrameCount > 0)
                    stackTrace.Append(builder);

                //if (exception is AggregateException aggEx)
                //    foreach (var ex in EnumerableIList.Create(aggEx.InnerExceptions))
                //        builder.AppendInnerException(ex);

                if (exception.InnerException != null)
                    builder.AppendInnerException(exception.InnerException);
            }
            catch
            {
                // Processing exceptions shouldn't throw exceptions; if it fails
            }

            return builder;
        }

        internal static StringBuilder AppendFormattingChar(this StringBuilder builder, char c)
#if !APKD_STACKTRACE_NOFORMAT
            => builder.Append(c);
#else
            => builder;
#endif

        private static void AppendInnerException(this StringBuilder builder, Exception exception)
        {
            builder.Append(" ---> ")
                .AppendDemystified(exception)
                .Append('\n')
                .Append("   --- End of inner exception stack trace ---");
        }
    }
}