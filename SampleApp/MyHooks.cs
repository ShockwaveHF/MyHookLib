using MyStaticHookLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace SampleApp
{
    public static class MyHooks
    {
        [Hook(0x06000002, HookPosition.Prefix, HookInjectMode.InjectType, HookReturnAction.None, true)]
        public static void OnPreFoo(object instance, string someParameter)
        {
            Console.WriteLine("PreFoo called. Parameter: " + someParameter);
        }

        [Hook("TargetApp.Program", "Foo", HookPosition.Prefix, HookInjectMode.InjectType, HookReturnAction.CancelIfTrue, false)]
        public static bool OnCancelFoo(string someParameter)
        {
            return someParameter == "block";
        }

        [Hook("TargetApp.Program", "GetString", HookPosition.Postfix, HookInjectMode.InjectType, HookReturnAction.ReplaceReturn, false)]
        public static string OnReplaceReturn(string originalReturnValue)
        {
            return originalReturnValue + "_HOOKED";
        }
    }
}
