using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Collections.Generic;
using System.Linq;
namespace MyStaticHookLib
{
    public static class InjectHelper
    {
        public static TypeDef Inject(TypeDef typeDef, ModuleDef target)
        {
            InjectHelper.InjectContext injectContext = new InjectHelper.InjectContext(target);
            InjectHelper.PopulateContext(typeDef, injectContext);
            InjectHelper.Copy(typeDef, injectContext, true);
            return (TypeDef)injectContext.Mep[typeDef];
        }

        public static MethodDef Inject(MethodDef methodDef, ModuleDef target)
        {
            InjectHelper.InjectContext injectContext = new InjectHelper.InjectContext(target);
            injectContext.Mep[methodDef] = InjectHelper.Clone(methodDef);
            InjectHelper.InjectContext injectContext2 = injectContext;
            InjectHelper.CopyMethodDef(methodDef, injectContext2);
            return (MethodDef)injectContext2.Mep[methodDef];
        }

        public static IEnumerable<IDnlibDef> Inject(TypeDef typeDef, TypeDef newType, ModuleDef target)
        {
            InjectHelper.InjectContext injectContext = new InjectHelper.InjectContext(target);
            injectContext.Mep[typeDef] = newType;
            InjectHelper.InjectContext injectContext2 = injectContext;
            InjectHelper.PopulateContext(typeDef, injectContext2);
            InjectHelper.Copy(typeDef, injectContext2, false);
            return injectContext2.Mep.Values.Except(new TypeDef[] { newType });
        }

        private static TypeDefUser Clone(TypeDef origin)
        {
            TypeDefUser typeDefUser = new TypeDefUser(origin.Namespace, origin.Name)
            {
                Attributes = origin.Attributes
            };
            bool flag = origin.ClassLayout != null;
            if (flag)
            {
                typeDefUser.ClassLayout = new ClassLayoutUser(origin.ClassLayout.PackingSize, origin.ClassSize);
            }
            foreach (GenericParam genericParam in origin.GenericParameters)
            {
                typeDefUser.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));
            }
            return typeDefUser;
        }

        private static MethodDefUser Clone(MethodDef origin)
        {
            MethodDefUser methodDefUser = new MethodDefUser(origin.Name, null, origin.ImplAttributes, origin.Attributes);
            foreach (GenericParam genericParam in origin.GenericParameters)
            {
                methodDefUser.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));
            }
            return methodDefUser;
        }

        private static FieldDefUser Clone(FieldDef origin)
        {
            return new FieldDefUser(origin.Name, null, origin.Attributes);
        }

        private static void Copy(TypeDef typeDef, InjectHelper.InjectContext ctx, bool copySelf)
        {
            if (copySelf)
            {
                InjectHelper.CopyTypeDef(typeDef, ctx);
            }
            foreach (TypeDef typeDef2 in typeDef.NestedTypes)
            {
                InjectHelper.Copy(typeDef2, ctx, true);
            }
            foreach (MethodDef methodDef in typeDef.Methods)
            {
                InjectHelper.CopyMethodDef(methodDef, ctx);
            }
            foreach (FieldDef fieldDef in typeDef.Fields)
            {
                InjectHelper.CopyFieldDef(fieldDef, ctx);
            }
        }

        private static void CopyFieldDef(FieldDef fieldDef, InjectHelper.InjectContext ctx)
        {
            FieldDef fieldDef2 = (FieldDef)ctx.Mep[fieldDef];
            fieldDef2.Signature = ctx.Importer.Import(fieldDef.Signature);
        }

        private static void CopyMethodDef(MethodDef methodDef, InjectContext ctx)
        {
            MethodDef targetMethod = (MethodDef)ctx.Mep[methodDef];

            targetMethod.Signature = ctx.Importer.Import(methodDef.Signature);
            targetMethod.Parameters.UpdateParameterTypes();

            if (methodDef.ImplMap != null)
            {
                targetMethod.ImplMap = new ImplMapUser(
                    new ModuleRefUser(ctx.TargetModule, methodDef.ImplMap.Module.Name),
                    methodDef.ImplMap.Name,
                    methodDef.ImplMap.Attributes);
            }

            if (methodDef.HasBody)
            {
                targetMethod.Body = new CilBody(methodDef.Body.InitLocals, new List<Instruction>(), new List<ExceptionHandler>(), new List<Local>())
                {
                    MaxStack = methodDef.Body.MaxStack
                };

                Dictionary<object, object> bodyMap = new Dictionary<object, object>();

                foreach (Local local in methodDef.Body.Variables)
                {
                    Local newLocal = new Local(ctx.Importer.Import(local.Type));
                    targetMethod.Body.Variables.Add(newLocal);
                    newLocal.Name = local.Name;
                    newLocal.Attributes = local.Attributes;
                    bodyMap[local] = newLocal;
                }

                foreach (Instruction instr in methodDef.Body.Instructions)
                {
                    Instruction newInstr = new Instruction(instr.OpCode, instr.Operand)
                    {
                        SequencePoint = instr.SequencePoint
                    };

                    if (newInstr.Operand is IType type)
                    {
                        newInstr.Operand = ctx.Importer.Import(type);
                    }
                    else if (newInstr.Operand is IMethod method)
                    {
                        newInstr.Operand = ctx.Importer.Import(method);
                    }
                    else if (newInstr.Operand is IField field)
                    {
                        newInstr.Operand = ctx.Importer.Import(field);
                    }

                    targetMethod.Body.Instructions.Add(newInstr);
                    bodyMap[instr] = newInstr;
                }

                foreach (Instruction instr in targetMethod.Body.Instructions)
                {
                    if (instr.Operand != null && bodyMap.ContainsKey(instr.Operand))
                    {
                        instr.Operand = bodyMap[instr.Operand];
                    }
                    else if (instr.Operand is Instruction[] targets)
                    {
                        Instruction[] newTargets = new Instruction[targets.Length];
                        for (int i = 0; i < targets.Length; i++)
                        {
                            newTargets[i] = (Instruction)bodyMap[targets[i]];
                        }
                        instr.Operand = newTargets;
                    }
                }

                foreach (ExceptionHandler eh in methodDef.Body.ExceptionHandlers)
                {
                    targetMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
                    {
                        CatchType = (eh.CatchType == null) ? null : ctx.Importer.Import(eh.CatchType),
                        TryStart = (Instruction)bodyMap[eh.TryStart],
                        TryEnd = (Instruction)bodyMap[eh.TryEnd],
                        HandlerStart = (Instruction)bodyMap[eh.HandlerStart],
                        HandlerEnd = (Instruction)bodyMap[eh.HandlerEnd],
                        FilterStart = (eh.FilterStart == null) ? null : (Instruction)bodyMap[eh.FilterStart]
                    });
                }

                targetMethod.Body.SimplifyMacros(targetMethod.Parameters);
            }
        }

        private static void CopyTypeDef(TypeDef typeDef, InjectHelper.InjectContext ctx)
        {
            TypeDef typeDef2 = (TypeDef)ctx.Mep[typeDef];
            typeDef2.BaseType = ctx.Importer.Import(typeDef.BaseType);
            foreach (InterfaceImpl interfaceImpl in typeDef.Interfaces)
            {
                typeDef2.Interfaces.Add(new InterfaceImplUser(ctx.Importer.Import(interfaceImpl.Interface)));
            }
        }

        private static TypeDef PopulateContext(TypeDef typeDef, InjectHelper.InjectContext ctx)
        {
            IDnlibDef dnlibDef;
            bool flag = !ctx.Mep.TryGetValue(typeDef, out dnlibDef);
            TypeDef typeDef2;
            if (flag)
            {
                typeDef2 = InjectHelper.Clone(typeDef);
                ctx.Mep[typeDef] = typeDef2;
            }
            else
            {
                typeDef2 = (TypeDef)dnlibDef;
            }
            foreach (TypeDef typeDef3 in typeDef.NestedTypes)
            {
                typeDef2.NestedTypes.Add(InjectHelper.PopulateContext(typeDef3, ctx));
            }
            foreach (MethodDef methodDef in typeDef.Methods)
            {
                typeDef2.Methods.Add((MethodDef)(ctx.Mep[methodDef] = InjectHelper.Clone(methodDef)));
            }
            foreach (FieldDef fieldDef in typeDef.Fields)
            {
                typeDef2.Fields.Add((FieldDef)(ctx.Mep[fieldDef] = InjectHelper.Clone(fieldDef)));
            }
            return typeDef2;
        }
        private class InjectContext : ImportMapper
        {
            public InjectContext(ModuleDef target)
            {
                this.TargetModule = target;
                this.Importer = new Importer(target, ImporterOptions.TryToUseTypeDefs, default(GenericParamContext), this);
            }

            public Importer Importer { get; }

            // Token: 0x04000049 RID: 73
            public readonly Dictionary<IDnlibDef, IDnlibDef> Mep = new Dictionary<IDnlibDef, IDnlibDef>();

            // Token: 0x0400004A RID: 74
            public readonly ModuleDef TargetModule;

            public override ITypeDefOrRef Map(ITypeDefOrRef typeDefOrRef)
            {
                bool flag = typeDefOrRef is TypeDef;
                if (flag)
                {
                    TypeDef typeDef = (TypeDef)typeDefOrRef;
                    bool flag2 = this.Mep.ContainsKey(typeDef);
                    if (flag2)
                    {
                        return (TypeDef)this.Mep[typeDef];
                    }
                }
                return null;
            }

            // Token: 0x06000143 RID: 323 RVA: 0x00017658 File Offset: 0x00015858
            public override IMethod Map(MethodDef methodDef)
            {
                return this.Mep.ContainsKey(methodDef) ? (this.Mep[methodDef] as MethodDef) : null;
            }

            // Token: 0x06000144 RID: 324 RVA: 0x0001768C File Offset: 0x0001588C
            public override IField Map(FieldDef fieldDef)
            {
                return this.Mep.ContainsKey(fieldDef) ? (this.Mep[fieldDef] as FieldDef) : null;
            }
        }
    }
}