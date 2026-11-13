using dnlib.DotNet;
using dnlib.DotNet.Emit;

using dnlib.DotNet.Emit;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace MyStaticHookLib
{
    public sealed class HookPatcher
    {
        private readonly ModuleDefMD _targetModule;
        private readonly Dictionary<TypeDef, TypeDef> _injectedTypes;

        /// <summary>
        /// Инициализирует патчер, загружая целевую сборку с диска.
        /// </summary>
        /// <param name="targetAssemblyPath">Путь к целевому .exe или .dll файлу.</param>
        public HookPatcher(string targetAssemblyPath)
        {
            if (string.IsNullOrEmpty(targetAssemblyPath) || !File.Exists(targetAssemblyPath))
            {
                throw new FileNotFoundException("Target assembly not found: " + targetAssemblyPath, targetAssemblyPath);
            }

            _targetModule = ModuleDefMD.Load(targetAssemblyPath);
            _injectedTypes = new Dictionary<TypeDef, TypeDef>();
        }


        /// <summary>
        /// Инициализирует патчер, загружая целевую сборку из массива байтов.
        /// </summary>
        /// <param name="targetAssemblyBytes">Массив байтов целевой сборки.</param>
        public HookPatcher(byte[] targetAssemblyBytes)
        {
            if (targetAssemblyBytes == null || targetAssemblyBytes.Length == 0)
            {
                throw new ArgumentException("Target assembly bytes cannot be null or empty.", "targetAssemblyBytes");
            }

            _targetModule = ModuleDefMD.Load(targetAssemblyBytes);
            _injectedTypes = new Dictionary<TypeDef, TypeDef>();
        }

        /// <summary>
        /// Режим работы после применения хуков (запуск в памяти или сохранение на диск).
        /// </summary>
        public PatchOutputMode OutputMode { get; set; }

        /// <summary>
        /// Путь для сохранения файла, если OutputMode установлен в SaveToDisk.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Аргументы командной строки, передаваемые в EntryPoint при запуске в памяти.
        /// </summary>
        public string[] EntryArgs { get; set; }

        public void Execute()
        {
            if (OutputMode == PatchOutputMode.SaveToDisk)
            {
                if (string.IsNullOrEmpty(OutputPath))
                {
                    throw new InvalidOperationException("OutputPath is required when OutputMode is SaveToDisk.");
                }
                SaveToDisk(OutputPath);
                return;
            }

            ExecuteInMemory();
        }

        private void ExecuteInMemory()
        {
            byte[] patchedBytes = SaveToMemory();
            Assembly loadedAssembly = Assembly.Load(patchedBytes);
            MethodInfo entryPoint = loadedAssembly.EntryPoint;
            if (entryPoint != null)
            {
                string[] args = EntryArgs ?? new string[0];
                object[] parameters = entryPoint.GetParameters().Length == 0 ? null : new object[] { args };
                entryPoint.Invoke(null, parameters);
            }
        }

        public void ApplyHooksFromAssembly(string hooksAssemblyPath, string hookContainerTypeName)
        {
            ModuleDefMD hooksModule = ModuleDefMD.Load(hooksAssemblyPath);
            TypeDef hookContainer = hooksModule.Find(hookContainerTypeName, true);

            if (hookContainer == null)
            {
                throw new Exception("Hook container type not found: " + hookContainerTypeName);
            }

            foreach (MethodDef hookMethod in hookContainer.Methods)
            {
                if (!hookMethod.IsStatic)
                {
                    continue;
                }

                HookData data = ParseHookAttribute(hookMethod);
                if (data == null)
                {
                    continue;
                }

                MethodDef targetMethod;
                if (data.TargetToken != 0)
                {
                    targetMethod = FindTargetMethodByToken(data.TargetToken);
                }
                else
                {
                    targetMethod = FindTargetMethod(data.TargetType, data.TargetMethod);
                }

                if (targetMethod == null || !targetMethod.HasBody)
                {
                    throw new Exception("Target method not found or has no body.");
                }

                IMethod resolvedHook = ResolveHookMethod(hookContainer, hookMethod, data.InjectMode);
                PatchMethod(targetMethod, resolvedHook, data);
            }
        }

        public void ApplyHookByToken(
            uint targetToken,
            string hooksAssemblyPath,
            string hookContainerTypeName,
            string hookMethodName,
            HookPosition position = HookPosition.Prefix,
            HookInjectMode injectMode = HookInjectMode.InjectType,
            HookReturnAction returnAction = HookReturnAction.None,
            bool passInstance = false)
        {
            ModuleDefMD hooksModule = ModuleDefMD.Load(hooksAssemblyPath);
            TypeDef hookContainer = hooksModule.Find(hookContainerTypeName, true);
            if (hookContainer == null)
            {
                throw new Exception("Hook container type not found: " + hookContainerTypeName);
            }

            MethodDef hookMethod = null;
            foreach (MethodDef m in hookContainer.Methods)
            {
                if (m.IsStatic && m.Name == hookMethodName)
                {
                    hookMethod = m;
                    break;
                }
            }

            if (hookMethod == null)
            {
                throw new Exception("Hook method not found: " + hookMethodName);
            }

            MethodDef targetMethod = FindTargetMethodByToken(targetToken);
            if (targetMethod == null || !targetMethod.HasBody)
            {
                throw new Exception("Target method not found by token: 0x" + targetToken.ToString("X"));
            }

            HookData data = new HookData();
            data.TargetToken = targetToken;
            data.Position = position;
            data.InjectMode = injectMode;
            data.ReturnAction = returnAction;
            data.PassInstance = passInstance;

            IMethod resolvedHook = ResolveHookMethod(hookContainer, hookMethod, injectMode);
            PatchMethod(targetMethod, resolvedHook, data);
        }

        private MethodDef FindTargetMethodByToken(uint token)
        {
            IMDTokenProvider provider = _targetModule.ResolveToken(token);
            MethodDef targetMethod = provider as MethodDef;
            if (targetMethod != null)
            {
                ConsoleLogger.Print(
                    "Target method resolved by token 0x" + token.ToString("X") + ": " + targetMethod.FullName,
                    ConsoleColor.Cyan);
            }
            return targetMethod;
        }

        private HookData ParseHookAttribute(MethodDef method)
        {
            foreach (CustomAttribute attr in method.CustomAttributes)
            {
                if (attr.TypeFullName == typeof(HookAttribute).FullName)
                {
                    HookData data = new HookData();

                    if (attr.ConstructorArguments.Count == 5)
                    {
                        data.TargetToken = (uint)attr.ConstructorArguments[0].Value;
                        data.Position = (HookPosition)(int)attr.ConstructorArguments[1].Value;
                        data.InjectMode = (HookInjectMode)(int)attr.ConstructorArguments[2].Value;
                        data.ReturnAction = (HookReturnAction)(int)attr.ConstructorArguments[3].Value;
                        data.PassInstance = (bool)attr.ConstructorArguments[4].Value;
                        return data;
                    }

                    if (attr.ConstructorArguments.Count == 6)
                    {
                        data.TargetType = attr.ConstructorArguments[0].Value.ToString();
                        data.TargetMethod = attr.ConstructorArguments[1].Value.ToString();
                        data.Position = (HookPosition)(int)attr.ConstructorArguments[2].Value;
                        data.InjectMode = (HookInjectMode)(int)attr.ConstructorArguments[3].Value;
                        data.ReturnAction = (HookReturnAction)(int)attr.ConstructorArguments[4].Value;
                        data.PassInstance = (bool)attr.ConstructorArguments[5].Value;
                        return data;
                    }
                }
            }
            return null;
        }


        internal void SaveToDisk(string outputPath)
        {
            try
            {
                _targetModule.Write(outputPath);
                ConsoleLogger.Print(string.Format("Saved {0}", outputPath), ConsoleColor.Gray);
            }
            catch (Exception ex)
            {
                ConsoleLogger.Print(ex.ToString(), ConsoleColor.Red);
                throw;
            }
        }

        internal byte[] SaveToMemory()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                _targetModule.Write(ms);
                return ms.ToArray();
            }
        }

        private MethodDef FindTargetMethod(string typeFullName, string methodName)
        {
            TypeDef targetType = _targetModule.Find(typeFullName, true);
            if (targetType == null)
            {
                return null;
            }

            foreach (MethodDef method in targetType.Methods)
            {
                if (method.Name == methodName)
                {
                    return method;
                }
            }
            return null;
        }

        private IMethod ResolveHookMethod(TypeDef hookContainer, MethodDef hookMethod, HookInjectMode mode)
        {
            if (mode == HookInjectMode.ExternalReference)
            {
                Importer importer = new Importer(_targetModule);
                return importer.Import(hookMethod);
            }

            TypeDef injectedType;
            if (!_injectedTypes.TryGetValue(hookContainer, out injectedType))
            {
                injectedType = InjectHelper.Inject(hookContainer, _targetModule);
                _targetModule.Types.Add(injectedType);
                _injectedTypes[hookContainer] = injectedType;
            }

            foreach (MethodDef method in injectedType.Methods)
            {
                if (method.Name == hookMethod.Name && method.Parameters.Count == hookMethod.Parameters.Count)
                {
                    return method;
                }
            }

            throw new Exception("Injected hook method not found: " + hookMethod.Name);
        }

        private void PatchMethod(MethodDef targetMethod, IMethod hookMethod, HookData data)
        {
            int expectedArgs = targetMethod.Parameters.Count;
            if (!targetMethod.IsStatic && !data.PassInstance)
            {
                expectedArgs -= 1;
            }

            int actualHookArgs = hookMethod.MethodSig.Params.Count;
            if (data.Position == HookPosition.Postfix && data.ReturnAction == HookReturnAction.ReplaceReturn)
            {
                if (actualHookArgs != 1)
                {
                    throw new InvalidOperationException("Postfix ReplaceReturn hook must have exactly 1 parameter: " + hookMethod.FullName);
                }
            }
            else if (expectedArgs != actualHookArgs)
            {
                throw new InvalidOperationException(
                    "Hook parameter count mismatch for " + targetMethod.FullName +
                    ". Expected: " + expectedArgs + ", Actual: " + actualHookArgs);
            }

            CilBody body = targetMethod.Body;
            body.SimplifyBranches();

            if (data.Position == HookPosition.Prefix)
            {
                ApplyPrefix(targetMethod, hookMethod, data);
            }
            else
            {
                ApplyPostfix(targetMethod, hookMethod, data);
            }

            body.OptimizeBranches();
        }



        private void ApplyPrefix(MethodDef targetMethod, IMethod hookMethod, HookData data)
        {
            CilBody body = targetMethod.Body;
            Instruction firstOriginal = body.Instructions[0];
            List<Instruction> prefixInstructions = BuildArgumentLoadInstructions(targetMethod, data.PassInstance, false);

            prefixInstructions.Add(Instruction.Create(OpCodes.Call, hookMethod));

            bool hookHasReturn = hookMethod.MethodSig.RetType.FullName != "System.Void";

            if (data.ReturnAction == HookReturnAction.ReplaceReturn)
            {
                prefixInstructions.Add(Instruction.Create(OpCodes.Ret));
            }
            else if (data.ReturnAction == HookReturnAction.CancelIfTrue)
            {
                prefixInstructions.Add(Instruction.Create(OpCodes.Brfalse_S, firstOriginal));
                if (targetMethod.ReturnType.FullName != "System.Void")
                {
                    Local defaultRet = new Local(targetMethod.ReturnType);
                    body.Variables.Add(defaultRet);
                    prefixInstructions.Add(Instruction.Create(OpCodes.Ldloca_S, defaultRet));
                    prefixInstructions.Add(Instruction.Create(OpCodes.Initobj, targetMethod.ReturnType.ToTypeDefOrRef()));
                    prefixInstructions.Add(Instruction.Create(OpCodes.Ldloc_S, defaultRet));
                }
                prefixInstructions.Add(Instruction.Create(OpCodes.Ret));
            }
            else if (hookHasReturn)
            {
                prefixInstructions.Add(Instruction.Create(OpCodes.Pop));
            }

            for (int i = prefixInstructions.Count - 1; i >= 0; i--)
            {
                body.Instructions.Insert(0, prefixInstructions[i]);
            }
        }



        private void ApplyPostfix(MethodDef targetMethod, IMethod hookMethod, HookData data)
        {
            CilBody body = targetMethod.Body;
            List<Instruction> retInstructions = new List<Instruction>();

            foreach (Instruction instr in body.Instructions)
            {
                if (instr.OpCode == OpCodes.Ret)
                {
                    retInstructions.Add(instr);
                }
            }

            bool targetHasReturn = targetMethod.ReturnType.FullName != "System.Void";
            bool hookHasReturn = hookMethod.MethodSig.RetType.FullName != "System.Void";

            foreach (Instruction retInstr in retInstructions)
            {
                int index = body.Instructions.IndexOf(retInstr);
                List<Instruction> postfixInstructions = new List<Instruction>();
                Local returnLocal = null;

                if (targetHasReturn && data.ReturnAction != HookReturnAction.ReplaceReturn)
                {
                    returnLocal = new Local(targetMethod.ReturnType);
                    body.Variables.Add(returnLocal);
                    postfixInstructions.Add(Instruction.Create(OpCodes.Stloc_S, returnLocal));
                }

                if (data.ReturnAction != HookReturnAction.ReplaceReturn)
                {
                    List<Instruction> argLoads = BuildArgumentLoadInstructions(targetMethod, data.PassInstance, false);
                    postfixInstructions.AddRange(argLoads);
                }

                postfixInstructions.Add(Instruction.Create(OpCodes.Call, hookMethod));

                if (hookHasReturn && data.ReturnAction != HookReturnAction.ReplaceReturn)
                {
                    postfixInstructions.Add(Instruction.Create(OpCodes.Pop));
                }

                if (targetHasReturn && data.ReturnAction != HookReturnAction.ReplaceReturn)
                {
                    postfixInstructions.Add(Instruction.Create(OpCodes.Ldloc_S, returnLocal));
                }

                Instruction firstPostfixInstr = postfixInstructions[0];

                foreach (Instruction instr in body.Instructions)
                {
                    if (instr.Operand == retInstr)
                    {
                        instr.Operand = firstPostfixInstr;
                    }
                    else if (instr.Operand is Instruction[] targets)
                    {
                        for (int j = 0; j < targets.Length; j++)
                        {
                            if (targets[j] == retInstr)
                            {
                                targets[j] = firstPostfixInstr;
                            }
                        }
                    }
                }

                for (int i = 0; i < postfixInstructions.Count; i++)
                {
                    body.Instructions.Insert(index + i, postfixInstructions[i]);
                }
            }
        }


        private List<Instruction> BuildArgumentLoadInstructions(MethodDef targetMethod, bool passInstance, bool returnValueOnStack)
        {
            List<Instruction> list = new List<Instruction>();
            int startIndex = 0;

            if (!targetMethod.IsStatic)
            {
                if (passInstance)
                {
                    list.Add(Instruction.Create(OpCodes.Ldarg_0));
                }
                startIndex = 1;
            }

            for (int i = startIndex; i < targetMethod.Parameters.Count; i++)
            {
                list.Add(Instruction.Create(OpCodes.Ldarg, targetMethod.Parameters[i]));
            }

            return list;
        }
        private sealed class HookData
        {
            internal string TargetType;
            internal string TargetMethod;
            internal uint TargetToken;
            internal HookPosition Position;
            internal HookInjectMode InjectMode;
            internal HookReturnAction ReturnAction;
            internal bool PassInstance;
        }
    }
}