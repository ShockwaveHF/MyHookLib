using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace MyStaticHookLib
{
    internal static class ConsoleLogger
    {
        internal static void Print(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            ConsoleColor previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }
    }
}
