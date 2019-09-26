// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using DemystifyExceptions.Demystify.Enumerable;
using DemystifyExceptions.Demystify.Internal;

namespace DemystifyExceptions.Demystify
{
    internal sealed partial class EnhancedStackTrace : StackTrace, IEnumerable<EnhancedStackFrame>
    {
        private readonly List<EnhancedStackFrame> _frames;

        // Summary:
        //     Initializes a new instance of the System.Diagnostics.StackTrace class using the
        //     provided exception object.
        //
        // Parameters:
        //   e:
        //     The exception object from which to construct the stack trace.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     The parameter e is null.
        internal EnhancedStackTrace(Exception e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            _frames = GetFrames(e);
        }

        internal EnhancedStackTrace(StackTrace stackTrace)
        {
            if (stackTrace == null)
                throw new ArgumentNullException(nameof(stackTrace));

            _frames = GetFrames(stackTrace);
        }

        /// <summary>
        ///     Gets the number of frames in the stack trace.
        /// </summary>
        /// <returns>The number of frames in the stack trace.</returns>
        public override int FrameCount => _frames.Count;

        IEnumerator<EnhancedStackFrame> IEnumerable<EnhancedStackFrame>.GetEnumerator()
        {
            return _frames.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _frames.GetEnumerator();
        }

        internal static EnhancedStackTrace Current()
        {
            return new EnhancedStackTrace(new StackTrace(1 /* skip this one frame */, true));
        }

        /// <summary>
        ///     Gets the specified stack frame.
        /// </summary>
        /// <param name="index">The index of the stack frame requested.</param>
        /// <returns>The specified stack frame.</returns>
        public override StackFrame GetFrame(int index)
        {
            return _frames[index];
        }

        /// <summary>
        ///     Returns a copy of all stack frames in the current stack trace.
        /// </summary>
        /// <returns>
        ///     An array of type System.Diagnostics.StackFrame representing the function calls
        ///     in the stack trace.
        /// </returns>
        public override StackFrame[] GetFrames()
        {
            return _frames.ToArray();
        }

        /// <summary>
        ///     Builds a readable representation of the stack trace.
        /// </summary>
        /// <returns>A readable representation of the stack trace.</returns>
        public override string ToString()
        {
            return ToString(new StringBuilder());
        }

        public string ToString(StringBuilder sb)
        {
            if (_frames == null || _frames.Count == 0)
                return "";

            Append(sb);

            return sb.ToString();
        }


        internal void Append(StringBuilder sb)
        {
            var loggedFullFilepath = false;

            for (int i = 0, n = _frames.Count; i < n; i++)
            {
                sb.Append('\n');
                var frame = _frames[i];

                if (frame.IsEmpty)
                {
                    sb.Append(frame.StackFrame);
                }
                else
                {
                    frame.MethodInfo.Append(sb);

                    var filePath = frame.GetFileName();
                    if (!string.IsNullOrEmpty(filePath) && !frame.MethodInfo.Name.StartsWith("Log"))
                    {
#if !APKD_STACKTRACE_NOFORMAT
                        sb.Append(" →(at ");
#else
                        sb.Append(" (at ");
#endif
                        if (!loggedFullFilepath)
                        {
                            frame.AppendFullFilename(sb);
                            loggedFullFilepath = true;
                        }
                        else
                        {
                            sb.Append(filePath);
                        }

                        var lineNo = frame.GetFileLineNumber();
                        if (lineNo != 0)
                        {
                            sb.Append(':');
                            sb.Append(lineNo);
                            sb.Append(')');
                        }
                    }
                }
            }
        }

        private EnumerableIList<EnhancedStackFrame> GetEnumerator()
        {
            return EnumerableIList.Create(_frames);
        }
    }
}