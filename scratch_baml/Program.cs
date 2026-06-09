using System;
using System.IO;
using System.Resources;
using System.Reflection;
using ICSharpCode.BamlDecompiler;
using ICSharpCode.Decompiler.Metadata;

class Program
{
    static void Main()
    {
        try
        {
            string dllPath = @"c:\Users\Yunxi\Desktop\Econ\MotorTestSystem_backup.dll";
            var assembly = Assembly.LoadFrom(dllPath);
            string resourceName = "MotorTestSystem.g.resources";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Console.WriteLine("Could not find manifest resource: " + resourceName);
                    return;
                }
                
                using (var reader = new ResourceReader(stream))
                {
                    foreach (System.Collections.DictionaryEntry entry in reader)
                    {
                        string key = entry.Key.ToString() ?? "";
                        if (key.Equals("views/configview.baml", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("Found configview.baml, decompiling...");
                            Stream? valStream = entry.Value as Stream;
                            if (valStream == null && entry.Value is byte[] bytes)
                            {
                                valStream = new MemoryStream(bytes);
                            }
                            if (valStream != null)
                            {
                                var peFile = new PEFile(dllPath);
                                var resolver = new UniversalAssemblyResolver(dllPath, false, peFile.DetectTargetFrameworkId());
                                
                                // 添加项目输出目录作为搜索路径
                                resolver.AddSearchDirectory(@"C:\Users\Yunxi\Desktop\Econ\MotorTestSystem\bin\Debug\net8.0-windows");
                                
                                var settings = new BamlDecompilerSettings();
                                var decompiler = new XamlDecompiler(peFile, resolver, settings);
                                var result = decompiler.Decompile(valStream);
                                string xamlText = result.Xaml.ToString();
                                
                                string outPath = @"c:\Users\Yunxi\Desktop\Econ\ConfigView_Recovered.xaml";
                                File.WriteAllText(outPath, xamlText);
                                Console.WriteLine("Successfully decompiled to: " + outPath);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.ToString());
        }
    }
}
