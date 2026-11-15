using SharpTemplate;

string[] files = [/*"demo1.txt", "demo2.txt", "demo3.txt", "demo4.txt", "demo5.txt", "demo6.txt"*/];

foreach (var file in files)
{
    Console.WriteLine("{0}:", file);
    var template = File.ReadAllText(file);

    TemplateProcessor tp = new TemplateProcessor(template);
    var tmp = tp.Processing();

    Console.WriteLine(tmp);
    Console.WriteLine();
}

{
    var file = "demo7.txt";
    Console.WriteLine("{0}:", file);

    var player = new Player
    {
        Name = "Ivan"
    };
    var template = File.ReadAllText(file);
    var tp = new TemplateProcessor<Player>(template);
    var tmp = tp.Processing(player);

    Console.WriteLine(tmp);
    Console.WriteLine();
}

public class Player
{
    public string Name { get; set; } = "";

    public string Say(string text)
    {
        return $"{Name}: {text}";
    }
}