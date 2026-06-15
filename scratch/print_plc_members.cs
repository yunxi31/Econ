using System;
using System.Reflection;
using S7.Net;

class Program
{
    static void Main()
    {
        Console.WriteLine("Methods of S7.Net.Plc:");
        foreach (var m in typeof(Plc).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (m.IsVirtual)
            {
                Console.WriteLine($"Virtual: {m.ReturnType} {m.Name}({string.Join(", ", (object[])m.GetParameters())})");
            }
            else
            {
                Console.WriteLine($"Normal: {m.ReturnType} {m.Name}({string.Join(", ", (object[])m.GetParameters())})");
            }
        }
    }
}
