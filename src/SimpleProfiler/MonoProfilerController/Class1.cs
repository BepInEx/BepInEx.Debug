using System;
using UnityEngine;
using BepInEx;
using System.Runtime.InteropServices;

[BepInPlugin(nameof(AddDisabledLUT), nameof(AddDisabledLUT), "1.0")]
public class AddDisabledLUT : BaseUnityPlugin
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string fileName);

    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    delegate void Dump();
    Dump dumpMethod;

    void Awake()
    {
        var profiler = LoadLibrary("MonoProfiler.dll");

        if (profiler == IntPtr.Zero)
            return;

        var dump = GetProcAddress(profiler, "Dump");
        Console.WriteLine($"{nameof(dump)}: {dump}");

        if (dump == IntPtr.Zero)
            return;

        dumpMethod = (Dump)Marshal.GetDelegateForFunctionPointer(dump, typeof(Dump));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote)) dumpMethod();
    }
}