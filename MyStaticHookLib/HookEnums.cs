using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace MyStaticHookLib
{
    /// <summary>
    /// Позиция внедрения хука в тело целевого метода.
    /// </summary>
    public enum HookPosition
    {
        /// <summary>
        /// В начале метода (до выполнения оригинального кода).
        /// </summary>
        Prefix,

        /// <summary>
        /// В конце метода (перед каждой инструкцией возврата Ret).
        /// </summary>
        Postfix
    }

    /// <summary>
    /// Способ подключения кода хука к целевой сборке.
    /// </summary>
    public enum HookInjectMode
    {
        /// <summary>
        /// Создает внешнюю ссылку на сборку с хуками. Файл с хуками должен находиться рядом с целевой программой.
        /// </summary>
        ExternalReference,

        /// <summary>
        /// Копирует весь класс-контейнер с хуками внутрь целевой сборки. Внешние зависимости не требуются.
        /// </summary>
        InjectType
    }

    /// <summary>
    /// Действие с потоком выполнения и возвращаемым значением целевого метода.
    /// </summary>
    public enum HookReturnAction
    {
        /// <summary>
        /// Обычный вызов без изменения логики возврата. Хук должен возвращать void.
        /// </summary>
        None,

        /// <summary>
        /// Прерывает выполнение целевого метода (только для Prefix), если хук вернул true. Хук должен возвращать bool.
        /// </summary>
        CancelIfTrue,

        /// <summary>
        /// Подменяет возвращаемое значение целевого метода (только для Postfix). Хук должен принимать и возвращать тот же тип, что и целевой метод.
        /// </summary>
        ReplaceReturn
    }

    /// <summary>
    /// Режим вывода пропатченной сборки.
    /// </summary>
    public enum PatchOutputMode
    {
        /// <summary>
        /// Загрузить пропатченную сборку в память и выполнить ее EntryPoint.
        /// </summary>
        InMemory,

        /// <summary>
        /// Сохранить пропатченную сборку в файл на диск.
        /// </summary>
        SaveToDisk
    }
}
