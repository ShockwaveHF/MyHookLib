# MyHookLib

A lightweight .NET library for patching compiled third-party assemblies.

It injects your custom code into specified target methods, allowing you to build universal solutions without manually editing bytecode or dealing with low-level decompilation workflows.

## Features

- **Declarative Hooking:** Define hooks using standard attributes on static methods.
- **Flexible Targeting:** Resolve target methods by class/method names or directly via `MetadataToken`.
- **Execution Control:** Insert hooks at the start (`Prefix`) or end (`Postfix`) of a method, validate conditions, and replace return values.
- **Instance Access:** Capture the current `this` instance of non-static target methods.
- **Disk & In-Memory Modes:** Save the patched assembly to disk or execute it directly in memory.

## Quick Start

### 1. Define Your Hooks

Create a static class containing your hook methods. This code will be injected into the target assembly.

```csharp
using System.Reflection;
using MyStaticHookLib;

namespace SampleApp
{
    public static class MyHooks
    {
        [Hook("TargetApp.Program", "Foo", HookPosition.Prefix, HookInjectMode.InjectType, HookReturnAction.None, true)]
        public static void OnPreFoo(object instance, string someParameter)
        {
            // Your injected logic here
        }
    }
}
```

### 2. Apply Hooks and Run

Reference `MyHookLib` in your loader project, pass the target assembly path to `HookPatcher`, configure output settings, and apply your hooks:

```csharp
using System.Reflection;
using MyStaticHookLib;

namespace SampleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HookPatcher patcher = new HookPatcher("TargetApp.exe");
            patcher.OutputMode = PatchOutputMode.SaveToDisk;
            patcher.OutputPath = "TargetApp_Patched.exe";

            patcher.ApplyHooksFromAssembly(Assembly.GetExecutingAssembly().Location, typeof(MyHooks).FullName);
            patcher.Patch();
        }
    }
}
```

## `[Hook]` Attribute Parameters

1. **Target Class & Method (or `MetadataToken`):** Specify the full name of the target class and method name. If you do not want to rely on string names (e.g., for obfuscated assemblies), you can specify the `MetadataToken` instead.
2. **`HookPosition`:** Defines where the hook is placed (`Prefix` — beginning of the method, `Postfix` — end of the method).
3. **`HookInjectMode`:** Defines how the hook code is injected into the assembly (`HookInjectMode.InjectType` is recommended).
4. **`HookReturnAction`:** Controls execution flow and return values when performing checks inside the hook (e.g., canceling original execution or replacing the return value).
5. **`PassInstance` (Last Argument):** Set to `true` to receive the current `this` instance of the intercepted non-static class as the first parameter (`object instance`) of your hook method.
