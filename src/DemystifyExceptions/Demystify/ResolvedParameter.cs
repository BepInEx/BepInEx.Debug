// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using DemystifyExceptions.Demystify.Internal;

namespace DemystifyExceptions.Demystify
{
    internal class ResolvedParameter
    {
        internal string Name { get; set; }

        internal Type ResolvedType { get; set; }

        internal string Prefix { get; set; }
        internal string Prefix2 { get; set; }

        public override string ToString()
        {
            return Append(new StringBuilder()).ToString();
        }

        internal StringBuilder Append(StringBuilder sb)
        {
            sb.AppendFormattingChar('‹');

            if (!string.IsNullOrEmpty(Prefix2))
                sb.Append(Prefix2).Append(' ');

            if (!string.IsNullOrEmpty(Prefix))
                sb.Append(Prefix).Append(' ');

            if (ResolvedType != null)
                AppendTypeName(sb);
            else
                sb.Append('?');

            sb.AppendFormattingChar('›');

            if (!string.IsNullOrEmpty(Name))
                sb.Append(' ').Append(Name);

            return sb;
        }

        internal virtual void AppendTypeName(StringBuilder sb)
        {
            sb.AppendTypeDisplayName(ResolvedType, false, true);
        }
    }
}