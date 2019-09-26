// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using DemystifyExceptions.Demystify.Enumerable;
using DemystifyExceptions.Demystify.Internal;

namespace DemystifyExceptions.Demystify
{
    internal sealed class ResolvedMethod
    {
        internal MethodBase MethodBase { get; set; }

        internal Type DeclaringType { get; set; }

        internal bool IsLambda { get; set; }

        internal ResolvedParameter ReturnParameter { get; set; }

        internal string Name { get; set; }

        internal int? Ordinal { get; set; }

        internal string GenericArguments { get; set; }

        internal Type[] ResolvedGenericArguments { get; set; }

        internal MethodBase SubMethodBase { get; set; }

        internal string SubMethod { get; set; }

        internal EnumerableIList<ResolvedParameter> Parameters { get; set; }

        internal EnumerableIList<ResolvedParameter> SubMethodParameters { get; set; }

        public override string ToString()
        {
            return Append(new StringBuilder()).ToString();
        }

        internal StringBuilder Append(StringBuilder builder)
        {
            if (ReturnParameter != null)
            {
                //if (IsAsync)
                //    ReturnParameter.Prefix2 = "async";

                ReturnParameter.Append(builder);
                builder.Append(' ');
            }

            var hasSubMethodOrLambda = !string.IsNullOrEmpty(SubMethod) || IsLambda;

            if (DeclaringType != null)
            {
                if (Name == ".ctor")
                {
                    if (!hasSubMethodOrLambda)
                    {
                        builder
                            .Append(".new ");

                        AppendDeclaringTypeName(builder)
                            .Append(Name);
                    }
                }
                else if (Name == ".cctor")
                {
                    builder.Append("static ");

                    AppendDeclaringTypeName(builder);
                }
                else
                {
                    AppendDeclaringTypeName(builder)
                        .Append('.')
                        .Append(Name);
                }
            }
            else
            {
                builder
                    .Append('.')
                    .Append(Name);
            }

            builder.Append(GenericArguments);

            if (!hasSubMethodOrLambda)
                builder.AppendFormattingChar('‼');

#if !APKD_STACKTRACE_HIDEPARAMS
#if APKD_STACKTRACE_FULLPARAMS
            builder.Append('(');
            if (MethodBase != null)
            {
                var isFirst = true;
                foreach (var param in Parameters)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        builder.Append(',').Append(' ');

                    param.Append(builder);
                }
            }
            else
            {
                builder.Append('?');
            }
            builder.Append(')');
#elif APKD_STACKTRACE_SHORTPARAMS
            char GetParamAlphabeticalName(int index) => (char)((int)'a' + index);
            char? GetParamNameFirstLetter(ResolvedParameter param) => string.IsNullOrEmpty(param?.Name) ? null as char? : param.Name[0];

            builder.Append('(');
            if (MethodBase != null)
            {
                var isFirst = true;
                builder.AppendFormattingChar('‹');
                for (int i = 0, n = Parameters.Count; i < n; ++i)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        builder.Append(',').Append(' ');

                    builder.Append(GetParamNameFirstLetter(Parameters[i]) ?? GetParamAlphabeticalName(i));
                }
                builder.AppendFormattingChar('›');
            }
            else
            {
                builder.Append('?');
            }
            builder.Append(')');
#else
            builder.Append('(');
            if (MethodBase != null)
            {
                var isFirst = true;
                builder.AppendFormattingChar('‹');
                foreach (var param in Parameters)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        builder.Append(',').Append(' ');

                    param.AppendTypeName(builder);
                }

                builder.AppendFormattingChar('›');
            }
            else
            {
                builder.Append('?');
            }

            builder.Append(')');
#endif
#endif

            if (hasSubMethodOrLambda)
            {
                builder.Append('+');
                builder.Append(SubMethod);
                if (IsLambda)
                {
                    builder.Append('(');
                    if (SubMethodBase != null)
                    {
                        var isFirst = true;
                        builder.AppendFormattingChar('‹');
                        foreach (var param in SubMethodParameters)
                        {
                            if (isFirst)
                                isFirst = false;
                            else
                                builder.Append(',').Append(' ');

                            param.AppendTypeName(builder);
                        }

                        builder.AppendFormattingChar('›');
                    }
                    else
                    {
                        builder.Append('?');
                    }

                    builder.Append(")➞ ");

                    var returnType = (SubMethodBase as MethodInfo)?.ReturnType;
                    if (returnType != null)
                        builder.AppendTypeDisplayName(returnType, false);
                    else
                        builder.Append("{…}");

                    if (Ordinal.HasValue)
                    {
                        builder.Append(' ');
                        builder.Append('[');
                        builder.Append(Ordinal.Value);
                        builder.Append(']');
                    }

                    builder.AppendFormattingChar('‼');
                }
                else
                {
                    builder.AppendFormattingChar('‼');
                    builder.Append('(');
                    var isFirst = true;
                    builder.AppendFormattingChar('‹');
                    foreach (var param in SubMethodParameters)
                    {
                        if (isFirst)
                            isFirst = false;
                        else
                            builder.Append(',').Append(' ');

                        param.AppendTypeName(builder);
                    }

                    builder.AppendFormattingChar('›');
                    builder.Append(')');
                }
            }

            return builder;
        }

        private StringBuilder AppendDeclaringTypeName(StringBuilder builder)
        {
            return DeclaringType != null ? builder.AppendTypeDisplayName(DeclaringType, true, true) : builder;
        }
    }
}