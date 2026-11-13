using MyStaticHookLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace SampleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HookPatcher patcher = new HookPatcher("TargetApp.exe");
            patcher.OutputMode = PatchOutputMode.SaveToDisk;
            patcher.OutputPath = "TargetApp_Patched.exe";
            patcher.EntryArgs = args;

            patcher.ApplyHooksFromAssembly(Assembly.GetExecutingAssembly().Location, typeof(MyHooks).FullName);
            patcher.Execute();
        }
    }
}
