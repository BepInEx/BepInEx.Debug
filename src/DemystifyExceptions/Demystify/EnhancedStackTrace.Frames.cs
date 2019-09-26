// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using DemystifyExceptions.Demystify.Enumerable;
using DemystifyExceptions.Demystify.Internal;
using static System.Reflection.BindingFlags;

namespace DemystifyExceptions.Demystify
{
    internal sealed partial class EnhancedStackTrace
    {
        private static readonly Type StackTraceHiddenAttibuteType =
            Type.GetType("System.Diagnostics.StackTraceHiddenAttribute", false);
        //static readonly MethodInfo UnityEditorInspectorWindowOnGuiMethod = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow", false)?.GetMethod("OnGUI", NonPublic | Instance);

        [ThreadStatic] private static StringBuilder _strBuilder;

        private static readonly CacheDictionary<ICustomAttributeProvider, object[]> customAttributeCache
            = new CacheDictionary<ICustomAttributeProvider, object[]>();

        private static readonly CacheDictionary<MethodBase, ResolvedMethod> resolvedMethodCache
            = new CacheDictionary<MethodBase, ResolvedMethod>();

        private static readonly FieldInfo capturedTracesFieldInfo =
            typeof(StackTrace).GetField("captured_traces", NonPublic | Instance);

        private static StringBuilder TempStringBuilder => _strBuilder ?? (_strBuilder = new StringBuilder(256));

        private static List<EnhancedStackFrame> GetFrames(Exception exception)
        {
            if (exception == null)
                return new List<EnhancedStackFrame>();

            var needFileInfo = true;
            var stackTrace = new StackTrace(exception, needFileInfo);

            return GetFrames(stackTrace);
        }

        private static StackTrace[] GetInnerStackTraces(StackTrace st)
        {
            return capturedTracesFieldInfo?.GetValue(st) as StackTrace[] ?? new StackTrace[0];
        }

        private static List<EnhancedStackFrame> GetFrames(StackTrace stackTrace)
        {
            IEnumerable<EnhancedStackFrame> EnumerateEnhancedFrames(StackTrace st)
            {
                IEnumerable<StackFrame> EnumerateFrames(StackTrace st2)
                {
                    foreach (var inner in GetInnerStackTraces(st2))
                    foreach (var frame in EnumerateFrames(inner))
                        yield return frame;

                    for (var i = 0; i < st2.FrameCount; i++)
                        yield return st2.GetFrame(i);
                }

                EnhancedStackFrame MakeEnhancedFrame(StackFrame frame, MethodBase method)
                {
                    return new EnhancedStackFrame(
                        frame,
                        GetResolvedMethod(method),
                        frame.GetFileName(),
                        frame.GetFileLineNumber(),
                        frame.GetFileColumnNumber());
                }

                var collapseNext = false;
                StackFrame current = null;

                foreach (var next in EnumerateFrames(stackTrace))
                    try
                    {
                        if (current != null)
                        {
                            var method = current.GetMethod();

                            if (method == null) // TODO: remove this
                                continue;

                            var shouldExcludeFromCollapse = ShouldExcludeFromCollapse(method);
                            if (shouldExcludeFromCollapse)
                                collapseNext = false;

                            if ((collapseNext || ShouldCollapseStackFrames(method)) && !shouldExcludeFromCollapse)
                            {
                                if (ShouldCollapseStackFrames(next.GetMethod()))
                                {
                                    collapseNext = true;
                                    continue;
                                }
                                else
                                {
                                    collapseNext = false;
                                }
                            }

                            if (!ShouldShowInStackTrace(method))
                                continue;

                            yield return MakeEnhancedFrame(current, method);
                        }
                    }
                    finally
                    {
                        current = next;
                    }

                if (current != null)
                    yield return MakeEnhancedFrame(current, current.GetMethod());
            }

            var resultList = new List<EnhancedStackFrame>(stackTrace.FrameCount);
            foreach (var item in EnumerateEnhancedFrames(stackTrace))
                resultList.Add(item);
            return resultList;
        }

        private static bool IsDefined<T>(MemberInfo member)
        {
            foreach (var attr in GetCustomAttributes(member))
                if (attr is T)
                    return true;
            return false;
        }

        private static object[] GetCustomAttributes(ICustomAttributeProvider obj)
        {
            return customAttributeCache.GetOrInitializeValue(obj, x => x.GetCustomAttributes(false));
        }

        internal static ResolvedMethod GetResolvedMethod(MethodBase methodBase)
        {
            return resolvedMethodCache.GetOrInitializeValue(methodBase, x => GetResolvedMethodInternal(methodBase));
        }

        internal static ResolvedMethod GetResolvedMethodInternal(MethodBase methodBase)
        {
            // Special case: no method available
            if (methodBase == null)
                return null;

            var method = methodBase;

            var methodDisplayInfo = new ResolvedMethod {SubMethodBase = method};

            // Type name
            var type = method.DeclaringType;

            var subMethodName = method.Name;
            var methodName = method.Name;

            if (type != null && IsDefined<CompilerGeneratedAttribute>(type) &&
                typeof(IEnumerator).IsAssignableFrom(type))
            {
                // Convert StateMachine methods to correct overload +MoveNext()
                if (!TryResolveStateMachineMethod(ref method, out type))
                {
                    methodDisplayInfo.SubMethodBase = null;
                    subMethodName = null;
                }

                methodName = method.Name;
            }

            // Method name
            methodDisplayInfo.MethodBase = method;
            methodDisplayInfo.Name = methodName;
            if (method.Name.IndexOf('<') >= 0)
            {
                if (TryResolveGeneratedName(ref method, out type, out methodName, out subMethodName, out var kind,
                    out var ordinal))
                {
                    methodName = method.Name;
                    methodDisplayInfo.MethodBase = method;
                    methodDisplayInfo.Name = methodName;
                    methodDisplayInfo.Ordinal = ordinal;
                }
                else
                {
                    methodDisplayInfo.MethodBase = null;
                }

                methodDisplayInfo.IsLambda = kind == GeneratedNameKind.LambdaMethod;

                if (methodDisplayInfo.IsLambda && type != null)
                    if (methodName == ".cctor")
                    {
                        if (type.IsGenericTypeDefinition && !(type.IsGenericType && !type.IsGenericTypeDefinition))
                        {
                            // TODO: diagnose type's generic type arguments from frame's "this" or something
                        }
                        else
                        {
                            var fields = type.GetFields(Static | Public | NonPublic);
                            foreach (var field in fields)
                            {
                                var value = field.GetValue(field);
                                if (value is Delegate d)
                                    if (ReferenceEquals(d.Method, methodBase) &&
                                        d.Target.ToString() == methodBase.DeclaringType.ToString())
                                    {
                                        methodDisplayInfo.Name = field.Name;
                                        methodDisplayInfo.IsLambda = false;
                                        method = methodBase;
                                        break;
                                    }
                            }
                        }
                    }
            }

            if (subMethodName != methodName)
                methodDisplayInfo.SubMethod = subMethodName;

            // ResolveStateMachineMethod may have set declaringType to null
            if (type != null)
                methodDisplayInfo.DeclaringType = type;

            if (method is MethodInfo mi)
            {
                var returnParameter = mi.ReturnParameter;
                if (returnParameter != null)
                    methodDisplayInfo.ReturnParameter = GetParameter(mi.ReturnParameter);
                else if (mi.ReturnType != null)
                    methodDisplayInfo.ReturnParameter = new ResolvedParameter
                    {
                        Prefix = "",
                        Name = "",
                        ResolvedType = mi.ReturnType
                    };
            }

            if (method.IsGenericMethod)
            {
                var genericArguments = method.GetGenericArguments();
                var builder = TempStringBuilder.Clear();

                builder.Append('<');

                for (var i = 0; i < genericArguments.Length; ++i)
                {
                    if (i > 0)
                        builder.Append(',').Append(' ');

                    builder.AppendTypeDisplayName(genericArguments[i], false, true);
                }

                builder.Append('>');

                methodDisplayInfo.GenericArguments = builder.ToString();
                methodDisplayInfo.ResolvedGenericArguments = genericArguments;
            }

            // Method parameters
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                var parameterList = new List<ResolvedParameter>(parameters.Length);
                foreach (var parameter in parameters)
                    parameterList.Add(GetParameter(parameter));

                methodDisplayInfo.Parameters = parameterList;
            }

            if (methodDisplayInfo.SubMethodBase == methodDisplayInfo.MethodBase)
            {
                methodDisplayInfo.SubMethodBase = null;
            }
            else if (methodDisplayInfo.SubMethodBase != null)
            {
                parameters = methodDisplayInfo.SubMethodBase.GetParameters();
                if (parameters.Length > 0)
                {
                    var parameterList = new List<ResolvedParameter>(parameters.Length);
                    foreach (var parameter in parameters)
                    {
                        var param = GetParameter(parameter);
                        if (param.Name?.StartsWith("<") ?? true) continue;

                        parameterList.Add(param);
                    }

                    methodDisplayInfo.SubMethodParameters = parameterList;
                }
            }

            return methodDisplayInfo;
        }

        private static bool TryResolveGeneratedName(ref MethodBase method, out Type type, out string methodName,
            out string subMethodName, out GeneratedNameKind kind, out int? ordinal)
        {
            kind = GeneratedNameKind.None;
            type = method.DeclaringType;
            subMethodName = null;
            ordinal = null;
            methodName = method.Name;

            var generatedName = methodName;

            if (!TryParseGeneratedName(generatedName, out kind, out var openBracketOffset, out var closeBracketOffset))
                return false;

            methodName = generatedName.Substring(openBracketOffset + 1, closeBracketOffset - openBracketOffset - 1);

            switch (kind)
            {
                case GeneratedNameKind.LocalFunction:
                    var localNameStart = generatedName.IndexOf((char) kind, closeBracketOffset + 1);
                    if (localNameStart < 0)
                        break;
                    localNameStart += 3;

                    if (localNameStart < generatedName.Length)
                    {
                        var localNameEnd = generatedName.IndexOf("|", localNameStart);
                        if (localNameEnd > 0)
                            subMethodName = generatedName.Substring(localNameStart, localNameEnd - localNameStart);
                    }

                    break;
                case GeneratedNameKind.LambdaMethod:
                    subMethodName = "";
                    break;
            }

            var dt = method.DeclaringType;
            if (dt == null)
                return false;

            var matchHint = GetMatchHint(kind, method);

            var matchName = methodName;

            var candidateMethods = dt.GetMethods(Public | NonPublic | Static | Instance | DeclaredOnly)
                .Where(m => m.Name == matchName);
            if (TryResolveSourceMethod(candidateMethods, kind, matchHint, ref method, ref type, out ordinal))
                return true;

            var candidateConstructors = dt.GetConstructors(Public | NonPublic | Static | Instance | DeclaredOnly)
                .Where(m => m.Name == matchName);
            if (TryResolveSourceMethod(candidateConstructors, kind, matchHint, ref method, ref type, out ordinal))
                return true;

            const int MaxResolveDepth = 10;
            for (var i = 0; i < MaxResolveDepth; i++)
            {
                dt = dt.DeclaringType;
                if (dt == null)
                    return false;

                candidateMethods = dt.GetMethods(Public | NonPublic | Static | Instance | DeclaredOnly)
                    .Where(m => m.Name == matchName);
                if (TryResolveSourceMethod(candidateMethods, kind, matchHint, ref method, ref type, out ordinal))
                    return true;

                candidateConstructors = dt.GetConstructors(Public | NonPublic | Static | Instance | DeclaredOnly)
                    .Where(m => m.Name == matchName);
                if (TryResolveSourceMethod(candidateConstructors, kind, matchHint, ref method, ref type, out ordinal))
                    return true;

                if (methodName == ".cctor")
                {
                    candidateConstructors = dt.GetConstructors(Public | NonPublic | Static | DeclaredOnly)
                        .Where(m => m.Name == matchName);
                    foreach (var cctor in candidateConstructors)
                    {
                        method = cctor;
                        type = dt;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveSourceMethod<T>(IEnumerable<T> candidateMethods, GeneratedNameKind kind,
            string matchHint, ref MethodBase method, ref Type type, out int? ordinal) where T : MethodBase
        {
            ordinal = null;
            foreach (var candidateMethod in candidateMethods)
            {
                var methodBody = candidateMethod.GetMethodBody();
                if (kind == GeneratedNameKind.LambdaMethod)
                    foreach (var v in EnumerableIList.Create(methodBody?.LocalVariables))
                    {
                        if (v.LocalType == type)
                            GetOrdinal(method, ref ordinal);

                        method = candidateMethod;
                        type = method.DeclaringType;
                        return true;
                    }

                try
                {
                    var rawIL = methodBody?.GetILAsByteArray();
                    if (rawIL == null)
                        continue;
                    var reader = new ILReader(rawIL);
                    while (reader.Read(candidateMethod))
                        if (reader.Operand is MethodBase mb)
                            if (method == mb || matchHint != null && method.Name.Contains(matchHint))
                            {
                                if (kind == GeneratedNameKind.LambdaMethod)
                                    GetOrdinal(method, ref ordinal);

                                method = candidateMethod;
                                type = method.DeclaringType;
                                return true;
                            }
                }
                catch
                {
                    // https://github.com/benaadams/Ben.Demystifier/issues/32
                    // Skip methods where il can't be interpreted
                }
            }

            return false;
        }

        private static void GetOrdinal(MethodBase method, ref int? ordinal)
        {
#if APKD_STACKTRACE_LAMBDAORDINALS
            var lamdaStart = method.Name.IndexOf((char) GeneratedNameKind.LambdaMethod + "__") + 3;
            if (lamdaStart > 3)
            {
                var secondStart = method.Name.IndexOf('_', lamdaStart) + 1;
                if (secondStart > 0) lamdaStart = secondStart;

                if (!int.TryParse(method.Name.Substring(lamdaStart), out var foundOrdinal))
                {
                    ordinal = null;
                    return;
                }

                ordinal = foundOrdinal;

                var methods = method.DeclaringType.GetMethods(Public | NonPublic | Static | Instance | DeclaredOnly);
                var startName = method.Name.Substring(0, lamdaStart);
                var count = 0;
                foreach (var m in methods)
                    if (m.Name.Length > lamdaStart && m.Name.StartsWith(startName))
                    {
                        count++;

                        if (count > 1)
                            break;
                    }

                if (count <= 1)
                    ordinal = null;
            }
#endif
        }

        private static string GetMatchHint(GeneratedNameKind kind, MethodBase method)
        {
            var methodName = method.Name;

            switch (kind)
            {
                case GeneratedNameKind.LocalFunction:
                    var start = methodName.IndexOf("|");
                    if (start < 1)
                        return null;
                    var end = methodName.IndexOf("_", start) + 1;
                    if (end <= start)
                        return null;

                    return methodName.Substring(start, end - start);
            }

            return null;
        }

        // Parse the generated name. Returns true for names of the form
        // [CS$]<[middle]>c[__[suffix]] where [CS$] is included for certain
        // generated names, where [middle] and [__[suffix]] are optional,
        // and where c is a single character in [1-9a-z]
        // (csharp\LanguageAnalysis\LIB\SpecialName.cpp).
        internal static bool TryParseGeneratedName(
            string name,
            out GeneratedNameKind kind,
            out int openBracketOffset,
            out int closeBracketOffset)
        {
            openBracketOffset = -1;

            if (name.StartsWith("CS$<", StringComparison.Ordinal))
                openBracketOffset = 3;
            else if (name.StartsWith("<", StringComparison.Ordinal))
                openBracketOffset = 0;

            if (openBracketOffset >= 0)
            {
                closeBracketOffset = IndexOfBalancedParenthesis(name, openBracketOffset, '>');
                if (closeBracketOffset >= 0 && closeBracketOffset + 1 < name.Length)
                {
                    int c = name[closeBracketOffset + 1];
                    if (c >= '1' && c <= '9' || c >= 'a' && c <= 'z') // Note '0' is not special.
                    {
                        kind = (GeneratedNameKind) c;
                        return true;
                    }
                }
            }

            kind = GeneratedNameKind.None;
            openBracketOffset = -1;
            closeBracketOffset = -1;
            return false;
        }


        private static int IndexOfBalancedParenthesis(string str, int openingOffset, char closing)
        {
            var opening = str[openingOffset];

            var depth = 1;
            for (var i = openingOffset + 1; i < str.Length; i++)
            {
                var c = str[i];
                if (c == opening)
                {
                    depth++;
                }
                else if (c == closing)
                {
                    depth--;

                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string GetPrefix(ParameterInfo parameter, Type parameterType)
        {
            if (parameter.IsOut)
                return "out";

            if (parameterType != null && parameterType.IsByRef)
            {
                var attribs = GetCustomAttributes(parameter);
                if (attribs?.Length > 0)
                    foreach (var attrib in attribs)
                        if (attrib is Attribute att && att.GetType().IsIsReadOnlyAttribute())
                            return "in";

                return "ref";
            }

            return string.Empty;
        }

        private static ResolvedParameter GetParameter(ParameterInfo parameter)
        {
            var parameterType = parameter.ParameterType;
            var prefix = GetPrefix(parameter, parameterType);

            if (parameterType == null)
                return new ResolvedParameter
                {
                    Prefix = prefix,
                    Name = parameter.Name,
                    ResolvedType = parameterType
                };

            if (parameterType.IsGenericType)
            {
                var customAttribs = GetCustomAttributes(parameter);

                Attribute tupleNameAttribute = null;
                foreach (var attr in customAttribs)
                    if (attr is Attribute tena && tena.IsTupleElementNameAttribute())
                        tupleNameAttribute = tena;

#if APKD_STACKTRACE_FULLPARAMS
                var tupleNames = tupleNameAttribute?.GetTransformNames();
#else
                var tupleNames = null as IList<string>;
#endif

                if (tupleNameAttribute != null)
                    return GetValueTupleParameter(tupleNames, prefix, parameter.Name, parameterType);
            }

            if (parameterType.IsByRef)
                parameterType = parameterType.GetElementType();

            return new ResolvedParameter
            {
                Prefix = prefix,
                Name = parameter.Name,
                ResolvedType = parameterType
            };
        }

        private static ResolvedParameter GetValueTupleParameter(IList<string> tupleNames, string prefix, string name,
            Type parameterType)
        {
            return new ValueTupleResolvedParameter
            {
                TupleNames = tupleNames,
                Prefix = prefix,
                Name = name,
                ResolvedType = parameterType
            };
        }

        private static string GetValueTupleParameterName(IList<string> tupleNames, Type parameterType)
        {
            var sb = new StringBuilder();
            sb.Append('(');
            var args = parameterType.GetGenericArguments();
            for (var i = 0; i < args.Length; i++)
            {
                if (i > 0)
                    sb.Append(',').Append(' ');


                sb.AppendTypeDisplayName(args[i], false, true);

                if (i >= tupleNames.Count)
                    continue;

                var argName = tupleNames[i];
                if (argName == null)
                    continue;

                sb.Append(' ');
                sb.Append(argName);
            }

            sb.Append(')');
            return sb.ToString();
        }

        private static bool ShouldCollapseStackFrames(MethodBase method)
        {
            var comparison = StringComparison.Ordinal;
            var typeName = method.DeclaringType.FullName;
            return typeName.StartsWith("UnityEditor.", comparison) ||
                   typeName.StartsWith("UnityEngine.", comparison) ||
                   typeName.StartsWith("System.", comparison) ||
                   typeName.StartsWith("UnityScript.Lang.", comparison) ||
                   typeName.StartsWith("Odin.Editor.", comparison) ||
                   typeName.StartsWith("Boo.Lang.", comparison);
        }

        private static bool ShouldExcludeFromCollapse(MethodBase method)
        {
            //if (method == UnityEditorInspectorWindowOnGuiMethod)
            //    return true;
            return false;
        }

        private static bool ShouldShowInStackTrace(MethodBase method)
        {
            Debug.Assert(method != null);
            var type = method.DeclaringType;

            //if (type == typeof(Task<>) && method.Name == "InnerInvoke")
            //    return false;

            //if (type == typeof(Task))
            //{
            //    switch (method.Name)
            //    {
            //        case "ExecuteWithThreadLocal":
            //        case "Execute":
            //        case "ExecutionContextCallback":
            //        case "ExecuteEntry":
            //        case "InnerInvoke":
            //            return false;
            //    }
            //}

            if (type == typeof(ExecutionContext))
                switch (method.Name)
                {
                    case "RunInternal":
                    case "Run":
                        return false;
                }

            if (StackTraceHiddenAttibuteType != null)
                // Don't show any methods marked with the StackTraceHiddenAttribute
                // https://github.com/dotnet/coreclr/pull/14652
                if (IsStackTraceHidden(method))
                    return false;

            if (type == null)
                return true;

            var typeFullName = type.FullName;

            if (StackTraceHiddenAttibuteType != null)
            {
                // Don't show any types marked with the StackTraceHiddenAttribute
                // https://github.com/dotnet/coreclr/pull/14652
                if (IsStackTraceHidden(type))
                    return false;
            }
            else
            {
                // Fallbacks for runtime pre-StackTraceHiddenAttribute
                //if (type == typeof(ExceptionDispatchInfo) && method.Name == "Throw")
                //{
                //    return false;
                //}
                //else if (type == typeof(TaskAwaiter) ||
                //    type == typeof(TaskAwaiter<>) ||
                //    type == typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter) ||
                //    type == typeof(ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter))
                //{
                //    switch (method.Name)
                //    {
                //        case "HandleNonSuccessAndDebuggerNotification":
                //        case "ThrowForNonSuccess":
                //        case "ValidateEnd":
                //        case "GetResult":
                //            return false;
                //    }
                //}
                /*else */
                if (typeFullName == "System.ThrowHelper") return false;
            }

            // collapse internal async frames
            if (typeFullName.StartsWith("System.Runtime.CompilerServices.Async", StringComparison.Ordinal))
                return false;

#if ODIN_INSPECTOR
            // support for the Sirenix.OdinInspector package
            if (typeFullName.StartsWith("Sirenix.OdinInspector", StringComparison.Ordinal))
                return false;
#endif

            // support for the Apkd.AsyncManager package
            if (typeFullName.StartsWith("Apkd.Internal.AsyncManager", StringComparison.Ordinal))
                return false;

            if (typeFullName.StartsWith("Apkd.Internal.Continuation`1", StringComparison.Ordinal))
                return false;

            // collapse internal unity logging methods
            if (typeFullName == "UnityEngine.DebugLogHandler")
                return false;

            if (typeFullName == "UnityEngine.Logger")
                return false;

            if (typeFullName == "UnityEngine.Debug")
                return false;

            return true;
        }

        private static bool IsStackTraceHidden(MemberInfo memberInfo)
        {
            if (!memberInfo.Module.Assembly.ReflectionOnly)
                foreach (var attr in GetCustomAttributes(memberInfo))
                {
                    if (attr.GetType() == StackTraceHiddenAttibuteType)
                        return true;
                    return false;
                }

            EnumerableIList<CustomAttributeData> attributes;
            try
            {
                attributes = EnumerableIList.Create(CustomAttributeData.GetCustomAttributes(memberInfo));
            }
            catch (NotImplementedException)
            {
                return false;
            }

            // reflection-only attribute, match on name
            foreach (var attribute in attributes)
                if (attribute.Constructor.DeclaringType.FullName == StackTraceHiddenAttibuteType.FullName)
                    return true;

            return false;
        }

        private static bool TryResolveStateMachineMethod(ref MethodBase method, out Type declaringType)
        {
            Debug.Assert(method != null);
            Debug.Assert(method.DeclaringType != null);

            declaringType = method.DeclaringType;

            var parentType = declaringType.DeclaringType;
            if (parentType == null)
                return false;

            var methods = parentType.GetMethods(Public | NonPublic | Static | Instance | DeclaredOnly);
            if (methods == null)
                return false;

            foreach (var candidateMethod in methods)
            {
                var attributes = GetCustomAttributes(candidateMethod);
                if (attributes == null)
                    continue;

                //foreach (var attr in attributes)
                //{
                //    if (attr is StateMachineAttribute sma && sma.StateMachineType == declaringType)
                //    {
                //        method = candidateMethod;
                //        declaringType = candidateMethod.DeclaringType;
                //        // Mark the iterator as changed; so it gets the + annotation of the original method
                //        // async statemachines resolve directly to their builder methods so aren't marked as changed
                //        return sma is IteratorStateMachineAttribute;
                //    }
                //}
            }

            return false;
        }

        internal enum GeneratedNameKind
        {
            None = 0,

            // Used by EE:
            ThisProxyField = '4',
            HoistedLocalField = '5',
            DisplayClassLocalOrField = '8',
            LambdaMethod = 'b',
            LambdaDisplayClass = 'c',
            StateMachineType = 'd',

            LocalFunction =
                'g', // note collision with Deprecated_InitializerLocal, however this one is only used for method names

            // Used by EnC:
            AwaiterField = 'u',
            HoistedSynthesizedLocalField = 's',

            // Currently not parsed:
            StateMachineStateField = '1',
            IteratorCurrentBackingField = '2',
            StateMachineParameterProxyField = '3',
            ReusableHoistedLocalField = '7',
            LambdaCacheField = '9',
            FixedBufferField = 'e',
            AnonymousType = 'f',
            TransparentIdentifier = 'h',
            AnonymousTypeField = 'i',
            AutoPropertyBackingField = 'k',
            IteratorCurrentThreadIdField = 'l',
            IteratorFinallyMethod = 'm',
            BaseMethodWrapper = 'n',
            AsyncBuilderField = 't',
            DynamicCallSiteContainerType = 'o',
            DynamicCallSiteField = 'p'
        }
    }
}