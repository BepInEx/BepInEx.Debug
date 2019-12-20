# BepInEx Debugging Tools

## DemystifyExceptions
Turns exceptions into a more readable format. Resolves enumerators, lambdas and other complex structures.  
Based on [Apkd.UnityDemystifier](https://github.com/apkd/Apkd.UnityDemystifier).

**NOTE:** This is a preloader patcher. Put the compiled DLL into `BepInEx/patchers`. Requires BepInEx 5 (or BepInEx 4 with MonoMod.RuntimeDetour).

## DnSpy debugging
https://github.com/risk-of-thunder/R2Wiki/wiki/Debugging-Your-Mods-With-dnSpy

## Runtime Unity Editor
In-game inspector, editor and interactive console for applications made with Unity3D game engine. It's designed for debugging and modding Unity games.  
https://github.com/ManlyMarco/RuntimeUnityEditor#readme

## ScriptEngine
Loads and reloads BepInEx plugins from the `BepInEx\scripts` folder. User can reload all of these plugins by pressing the keyboard shortcut defined in the config. Shortcut is F6 by default.  
Very useful for quickly developing plugins as you don't have to keep reopening the game to see your changes.

Remember to clean up after the old plugin version in case you need to. Things like harmony patches or loose GameObjects/MonoBehaviours remain after the plugin gets destroyed. Loose gameobjects and monobehaviours in this case are objects that are not attached to the parent scriptengine gameobject.

## Startup Profiler
Log and report the time spent in the `Start`, `Awake`, `Main`, `.ctor` and `.cctor` methods of each plugin.  
Also reports the total time spent in all plugins during these methods and the total time spent in chainloader so any performance improvements done through multithreading can be analyzed better.

**NOTE:** This is a preloader patcher. Put the compiled DLL into `BepInEx/patchers`. Requires BepInEx 5 (or BepInEx 4 with MonoMod.RuntimeDetour).
