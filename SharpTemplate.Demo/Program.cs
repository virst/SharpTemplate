using SharpTemplate;

string[] files = ["demo1.txt", "demo2.txt", "demo3.txt", "demo4.txt", "demo5.txt", "demo6.txt"];

foreach (var file in files)
{
    Console.WriteLine("{0}:", file);
    var template = File.ReadAllText(file);
    var tmp = TemplateProcessor.TemplateProces(template);
    Console.WriteLine(tmp);
    Console.WriteLine();
}