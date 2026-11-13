using System;
namespace TargetApp
{
    internal sealed class Program
    {
        private static void Main(string[] args)
        {

            Program app = new Program();

            app.Foo("hello");
            Console.WriteLine("---");

            app.Foo("block");
            Console.WriteLine("---");

            string result = app.GetString();
            Console.WriteLine("Result: " + result);
            Console.ReadLine();
        }

        internal void Foo(string someParameter)
        {
            Console.WriteLine("Original Foo body: " + someParameter);
        }

        internal string GetString()
        {
            return "OriginalValue";
        }
    }
}
