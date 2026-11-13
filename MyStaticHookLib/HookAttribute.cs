
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace MyStaticHookLib
{
    /// <summary>
    /// Помечает статический метод как хук для внедрения в целевую сборку.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class HookAttribute : Attribute
    {
        public string TargetType { get; private set; }
        public string TargetMethod { get; private set; }
        public uint TargetToken { get; private set; }
        public HookPosition Position { get; private set; }
        public HookInjectMode InjectMode { get; private set; }
        public HookReturnAction ReturnAction { get; private set; }
        public bool PassInstance { get; private set; }

        /// <summary>
        /// Настраивает хук по полному имени целевого класса и имени метода.
        /// </summary>
        /// <param name="targetType">Полное имя целевого класса с пространством имен (например, "TargetApp.Program").</param>
        /// <param name="targetMethod">Имя целевого метода.</param>
        /// <param name="position">Позиция вставки: Prefix (начало) или Postfix (конец).</param>
        /// <param name="injectMode">Режим внедрения: копирование класса внутрь или ссылка на внешнюю сборку.</param>
        /// <param name="returnAction">Управление потоком выполнения или подмена возвращаемого значения.</param>
        /// <param name="passInstance">Если true и целевой метод не static, передает экземпляр this первым параметром в хук (тип object).</param>
        public HookAttribute(
            string targetType,
            string targetMethod,
            HookPosition position = HookPosition.Prefix,
            HookInjectMode injectMode = HookInjectMode.InjectType,
            HookReturnAction returnAction = HookReturnAction.None,
            bool passInstance = false)
        {
            TargetType = targetType;
            TargetMethod = targetMethod;
            TargetToken = 0;
            Position = position;
            InjectMode = injectMode;
            ReturnAction = returnAction;
            PassInstance = passInstance;
        }

        /// <summary>
        /// Настраивает хук по токену метаданных (MDToken) целевого метода.
        /// </summary>
        /// <param name="targetToken">Токен метаданных целевого метода (например, 0x06000002).</param>
        /// <param name="position">Позиция вставки: Prefix (начало) или Postfix (конец).</param>
        /// <param name="injectMode">Режим внедрения: копирование класса внутрь или ссылка на внешнюю сборку.</param>
        /// <param name="returnAction">Управление потоком выполнения или подмена возвращаемого значения.</param>
        /// <param name="passInstance">Если true и целевой метод не static, передает экземпляр this первым параметром в хук (тип object).</param>
        public HookAttribute(
            uint targetToken,
            HookPosition position = HookPosition.Prefix,
            HookInjectMode injectMode = HookInjectMode.InjectType,
            HookReturnAction returnAction = HookReturnAction.None,
            bool passInstance = false)
        {
            TargetType = string.Empty;
            TargetMethod = string.Empty;
            TargetToken = targetToken;
            Position = position;
            InjectMode = injectMode;
            ReturnAction = returnAction;
            PassInstance = passInstance;
        }
    }
}
