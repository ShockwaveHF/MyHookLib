using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

[assembly: Obfuscation(Feature = "string encryption", Exclude = true)]
namespace MyStaticHookLib
{
    internal class ObfuscationAttributes
    {
    }
}
