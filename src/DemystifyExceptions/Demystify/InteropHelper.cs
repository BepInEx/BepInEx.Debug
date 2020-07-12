using System.Linq;
using System.Reflection;

namespace System.Diagnostics
{
    public static class InteropHelper
    {
        private static Type GetType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false, true))
                .FirstOrDefault(t => t != null);
        }

        public static class Types
        {
            public static readonly Type AggregateException = InteropHelper.GetType("System.AggregateException");

            public static readonly Type IAsyncStateMachine =
                InteropHelper.GetType("System.Runtime.CompilerServices.IAsyncStateMachine");

            public static readonly Type DynamicAttribute =
                InteropHelper.GetType("System.Runtime.CompilerServices.DynamicAttribute");

            public static readonly Type StateMachineAttribute =
                InteropHelper.GetType("System.Runtime.CompilerServices.StateMachineAttribute");

            public static readonly Type IteratorStateMachineAttribute =
                InteropHelper.GetType("System.Runtime.CompilerServices.IteratorStateMachineAttribute");

            public static readonly Type GenericTask = InteropHelper.GetType("System.Threading.Tasks.Task`1");
            public static readonly Type Task = InteropHelper.GetType("System.Threading.Tasks.Task");
            public static readonly Type GenericValueTask = InteropHelper.GetType("System.Threading.Tasks.ValueTask`1");

            public static readonly Type ExceptionDispatchInfo =
                InteropHelper.GetType("System.Runtime.ExceptionServices.ExceptionDispatchInfo");

            public static readonly Type GenericTaskAwaiter =
                InteropHelper.GetType("System.Runtime.CompilerServices.TaskAwaiter`1");

            public static readonly Type TaskAwaiter =
                InteropHelper.GetType("System.Runtime.CompilerServices.TaskAwaiter");

            public static readonly Type GenericValueTaskAwaiter =
                InteropHelper.GetType("System.Runtime.CompilerServices.ValueTaskAwaiter`1");

            public static readonly Type ValueTaskAwaiter =
                InteropHelper.GetType("System.Runtime.CompilerServices.TaskAwaiter");

            public static readonly Type GenericConfiguredValueTaskAwaitable_ConfiguredValueTaskAwaiter =
                InteropHelper.GetType(
                    "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1+ConfiguredValueTaskAwaiter");

            public static readonly Type ConfiguredValueTaskAwaitable_ConfiguredValueTaskAwaiter =
                InteropHelper.GetType(
                    "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter");

            public static readonly Type GenericConfiguredTaskAwaitable_ConfiguredTaskAwaiter =
                InteropHelper.GetType(
                    "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter");

            public static readonly Type ConfiguredTaskAwaitable_ConfiguredTaskAwaiter =
                InteropHelper.GetType("System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter");

            public static PropertyInfo StateMachineType = StateMachineAttribute?.GetProperty("StateMachineType");
            public static PropertyInfo InnerExceptions = AggregateException?.GetProperty("InnerExceptions");
        }
    }
}