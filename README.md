# BepInEx Debugging Tools
Tools and resources useful for developing and debugging BepInEx plugins.

You need to have at least [BepInEx 5,x](https://github.com/BepInEx/BepInEx) installed for most of the tools to work.

## Tools in this repo

### DemystifyExceptions
Turns exceptions into a more readable format. Resolves enumerators, lambdas and other complex structures.  
Based on [Ben.Demystifier](https://github.com/benaadams/Ben.Demystifier).

**How to use:** This is a preloader patcher. Put the compiled DLL into `BepInEx/patchers`. Requires BepInEx 5 (or BepInEx 4 with MonoMod.RuntimeDetour).

### MirrorInternalLogs

This preloader patcher allows to capture and mirror Unity internal debug logs (i.e. the contents of `output_log.txt`).  
Preloader patch provides a public event one can listen to which will receive all Unity logs, including internal debug logs that are only output to `output_log.txt`.

Unlike output log, which can be disabled in the game, this mirror will always capture said debug logs. If Unity already outputs `output_log.txt`, 
this plugin will simply create a copy of it in a more accessible place that `%APPDATA%`.

**How to use:** This is a preloader patcher. Put the compiled DLL into `BepInEx/patchers`.  
By default, logs are output to `unity_log.txt`. This is configurable via `MirrorInternalLogs.cfg` configuration file that gets generated on the first run.

### Simple Mono Profiler
A simple profiler that can be used in any Unity player build as long as it can run BepInEx 5.x. It can generate a .csv file with profiling results from an arbitrary length of time.

**How to use:** To install simply extract the release archive into the game root directory (folder structure is important. MonoProfiler32/64.dll files need to be right next to the game .exe and other dlls need to be inside BepInEx subfolders). MonoProfiler32.dll and MonoProfiler64.dll can both be copied to the game directory and only the correct one will be loaded.

**Warning:** If using a custom mono.dll (dnSpy debugging), the game might randomly hard crash while the profiler is running.

Check the config file or use ConfigurationManager to change the hotkey used to dump the collected profiler data (KeyCode.BackQuote by default). Dumps only include information that was captured since the last time a dump was triggered.

You can use LibreOffice Calc or Excel to view the dumped .csv results. Using Calc as example, open the .csv and import it with default options, then select columns A B and C, and click Data/AutoFilter. You can now click the arrows in 1st row to filter and sort the results.

**Warning:** The profiler always runs and will noticeably slow down the game. To turn the profiler off you have to close the game and rename MonoProfiler dll to something else like `_MonoProfiler32.dll`. You need the correct version of `MonoProfiler.dll` for your game (either 32 or 64 bit).

**Warning:** Due to the way allocations are measured, the provided numbers are just rough estimates. In particular, any allocations that happen in other threads during the runtime of the method are usually included in the number.

### ScriptEngine
Loads and reloads BepInEx plugins from the `BepInEx\scripts` folder. User can reload all of these plugins by pressing the keyboard shortcut defined in the config. Shortcut is F6 by default.  
Very useful for quickly developing plugins as you don't have to keep reopening the game to see your changes.

Remember to clean up after the old plugin version in case you need to. Things like harmony patches or loose GameObjects/MonoBehaviours remain after the plugin gets destroyed. Loose gameobjects and monobehaviours in this case are objects that are not attached to the parent scriptengine gameobject. For example:

```cs
private static Harmony _hi;

private void Awake()
{
    _hi = Harmony.CreateAndPatchAll(typeof(Hooks));
    LoadYourResources();
}

private void OnDestroy()
{
    _hi?.UnpatchSelf();
    UnloadYourResources();
}
```

If you want to debug plugins loaded by ScriptEngine you'll have to find the loaded assembly after every reload, since by necessity a new, renamed copy of the plugin assembly is loaded on every reload. You either have to find the loaded plugin in the modules view of your debugger (it will be named something like `data-xxxxxxxxxxxxxxx`, sorting by timestamp can help find it or you can use `Debugger.Break()` in your plugin's init), or you can turn on the `DumpAssemblies` setting and use the dropped dll files to debug (check the setting description for more info).

**How to use:** This is a plugin. Put the compiled DLL into `BepInEx/plugins`.

### Startup Profiler
Log and report the time spent in the `Start`, `Awake`, `Main`, `.ctor` and `.cctor` methods of each plugin.  
Also reports the total time spent in all plugins during these methods and the total time spent in chainloader so any performance improvements done through multithreading can be analyzed better.

**How to use:** This is a preloader patcher. Put the compiled DLL into `BepInEx/patchers`. Requires BepInEx 5 (or BepInEx 4 with MonoMod.RuntimeDetour).

### CtorShotgun
A tool for determining initialization order of game clases. Useful for finding an optimal BepInEx entry point. 

**How to use:** This is a preloader patcher. Put the compiled DLL into `BepInEx/patchers`. Requires BepInEx 5. Find what looks like a good time for BepInEx to start initializing plugins and put it into `BepInEx.cfg`. If things crash, it's too early. If game code runs before it can be patched, it's too late.

### ConstructorProfiler
A simple profiler that counts the amount of objects being created (it counts constructor hits). Useful for determining sources of unnecessary allocations and subsequent garbage pressure on the GC (garbage collector) which can result in random stutters. Unlike Simple Mono Profiler, this is implemented as a plugin that uses patches instead of a full-blown profiler.

**Warning:** This does not work all that well and requires tweaking of the source code to fit your use case. Simple Mono Profiler is much more powerful and able to do the same work, so use it if you can and simply filter its results by `.ctor`.

**How to use:** This is a preloader patcher. Put the compiled DLL into `BepInEx/plugins`. Requires BepInEx 5.

## External tools and resources

### How use dnSpy debugger
You can debug already built Unity players even if you don't have its source project.
https://github.com/risk-of-thunder/R2Wiki/wiki/Debugging-Your-Mods-With-dnSpy

### Runtime Unity Editor
In-game inspector, editor and interactive console for applications made with Unity3D game engine. It's designed for debugging and modding Unity games.  
https://github.com/ManlyMarco/RuntimeUnityEditor#readme

## Troubleshooting
- The BepInEx plugin manager object is destroyed early in certain games which aggressively clean up scenes, causing some plugins to not work. This can be fixed by setting the `HideManagerGameObject` option to `true` in `BepInEx/config/BepInEx.cfg`.
