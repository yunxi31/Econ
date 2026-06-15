using System;
using System.Reflection;
using S7.Net.Types;

class Program
{
    static void Main()
    {
        foreach (var prop in typeof(DataItem).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine($"Property: {prop.PropertyType.Name} {prop.Name} (CanWrite: {prop.CanWrite})");
        }
    }
}
