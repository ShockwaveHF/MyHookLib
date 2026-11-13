using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace MyStaticHookLib
{
    internal class HookHandler
    {
        private const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        internal MethodInfo OriginalMethod;
        internal MethodInfo ReplaceMethod;

        internal HookHandler(MethodInfo originalMethod, MethodInfo replaceMethod)
        {
            OriginalMethod = originalMethod;
            ReplaceMethod = replaceMethod;
        }

        internal static MethodInfo GetMetadataToken(string selectedAssembly, int token)
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (Assembly assembly in assemblies)
                {
                    string asmName = assembly.GetName().Name;
                    if (asmName != null && asmName.IndexOf(selectedAssembly, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        MethodBase resolved = assembly.ManifestModule.ResolveMethod(token);
                        MethodInfo methodInfo = resolved as MethodInfo;

                        if (methodInfo != null)
                        {
                            ConsoleLogger.Print(
                                "Method found: " + methodInfo.Name + ", Token: 0x" + token.ToString("X") + ", IsStatic: " + methodInfo.IsStatic + ", Private: " + methodInfo.IsPrivate,
                                ConsoleColor.Cyan);
                            return methodInfo;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                ConsoleLogger.Print(ex.ToString(), ConsoleColor.Red);
                throw;
            }
        }

        internal static MethodInfo ResolveMethod(MethodInfo sourceModule, Type[] typeParams)
        {
            if (sourceModule == null || sourceModule.DeclaringType == null)
            {
                ConsoleLogger.Print("Source patch method not found.", ConsoleColor.Red);
                return null;
            }

            if (typeParams == null)
            {
                return sourceModule.DeclaringType.GetMethod(sourceModule.Name, AllFlags);
            }

            return sourceModule.DeclaringType.GetMethod(sourceModule.Name, AllFlags, null, typeParams, null);
        }

        internal static MethodInfo ResolveMethod(string className, string methodName, Type[] typeParams)
        {
            Type targetType = FindType(className);
            if (targetType == null)
            {
                return null;
            }

            return ResolveMethod(targetType, methodName, typeParams, false);
        }

        internal static MethodInfo ResolveMethod(Type typeClass, string methodName, Type[] typeParams, bool isProperty = false)
        {
            if (typeClass == null)
            {
                return null;
            }

            if (isProperty)
            {
                PropertyInfo prop = typeClass.GetProperty(methodName, AllFlags);
                if (prop == null)
                {
                    return null;
                }
                return prop.GetGetMethod(true);
            }

            if (typeParams == null)
            {
                return typeClass.GetMethod(methodName, AllFlags);
            }

            return typeClass.GetMethod(methodName, AllFlags, null, typeParams, null);
        }

        internal void RunHook()
        {
            if (OriginalMethod == null)
            {
                ConsoleLogger.Print("Original method not found.", ConsoleColor.Red);
                return;
            }

            if (ReplaceMethod == null)
            {
                ConsoleLogger.Print("Hook method not found.", ConsoleColor.Red);
                return;
            }
        }

        private static Type FindType(string className)
        {
            Type directType = Type.GetType(className);
            if (directType != null)
            {
                return directType;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type found = assembly.GetType(className);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
