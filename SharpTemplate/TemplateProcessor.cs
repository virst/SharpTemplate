using MoonSharp.Interpreter;
using System.Numerics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace SharpTemplate
{
    public class TemplateProcessor
    {
        private readonly StringBuilder sb;
        private TemplateProcessor(StringBuilder sb)
        {
            this.sb = sb;
        }

        static TemplateProcessor()
        {
            UserData.RegisterType<TemplateProcessor>();
        }

        public void Echo(object o)
        {
            sb.Append(o);
        }

        public static string TemplateProces(string template, object? ob = null)
        {
            var sb = new StringBuilder();
            var script = new Script();
            script.Globals["this"] = UserData.Create(new TemplateProcessor(sb));
            var length = template.Length;
            template += "  ";
            var blockStack = new Stack<char>();
            var loopStack = new Stack<LoopObject>();
            var activeWalkStack = new Stack<bool>(); activeWalkStack.Push(true);
            var tmp = string.Empty;
            var freeLoop = false;
            for (int i = 0; i < length; i++)
            {
                if (template[i] == '<' && template[i + 1] == '?')
                {
                    tmp = string.Empty;
                    i += 2;
                    if (template[i] == 'e') //else 
                    {
                        if (!freeLoop && template.Substring(i).StartsWith("else>"))
                        {
                            var b = activeWalkStack.Pop();
                            activeWalkStack.Push(!b);
                            i += 4;
                        }
                        continue;
                    }
                    if (!activeWalkStack.Peek())
                    {
                        blockStack.Push(template[i]);
                        activeWalkStack.Push(false);
                        continue;
                    }
                    if (template[i] == '=' || template[i] == '#')
                    {
                        blockStack.Push(template[i]);
                        activeWalkStack.Push(true);
                        continue;
                    }

                    if (template[i] == '@' || template[i] == '?')
                    {
                        blockStack.Push(template[i]);
                        var bStart = template.IndexOf('(', i) + 1;
                        var bEnd = template.IndexOf(')', bStart);
                        var block = template.Substring(bStart, bEnd - bStart);

                        if (template[i] == '@')
                        {
                            if (loopStack.Count == 0 || loopStack.Peek().Pos != i - 3)
                            {
                                var metod = SplitNetod(block);
                                var result = script.DoString("return " + metod.param);
                                loopStack.Push(new LoopObject(i - 3, metod.metod, result.Table.Values.GetEnumerator()));
                            }
                            var lo = loopStack.Peek();
                            var next = lo.Enumerator.MoveNext();
                            freeLoop = !next;
                            activeWalkStack.Push(next);
                            if (next)
                                script.Globals[lo.FieldName] = lo.Enumerator.Current;
                            else
                                loopStack.Pop();
                        }

                        if (template[i] == '?')
                        {
                            var result = script.DoString("return " + block);
                            activeWalkStack.Push(result.Boolean);
                        }
                        i = bEnd;
                    }

                    continue;
                }

                if (template[i] == '?' && template[i + 1] == '>')
                {
                    if (blockStack.Count == 0)
                    {
#if DEBUG
                        Console.WriteLine(sb);
#endif
                        throw new Exception();
                    }
                    var blockType = blockStack.Pop();
                    i += 2;
                    if (!activeWalkStack.Pop()) continue;
                    if (blockType == '=')
                    {
                        var result = script.DoString("return " + tmp);
                        sb.Append(result);
                    }
                    if (blockType == '#')
                    {
                        script.DoString(tmp);
                    }
                    if (blockType == '@')
                    {
                        var lo = loopStack.Peek();
                        if (lo.Pos >= 0)
                            i = lo.Pos;
                    }
                    continue;
                }
                if (!activeWalkStack.Peek()) continue;
                if (template[i] == '\\') i++;
                if (blockStack.Count == 0 || blockStack.Peek() == '@' || blockStack.Peek() == '?')
                    sb.Append(template[i]);
                else
                    tmp += template[i];
            }
            return sb.ToString();
        }

        private static (string metod, string param) SplitNetod(string s)
        {
            return (metod: s.Substring(0, s.IndexOf(':')).Trim(),
                param: s.Substring(s.IndexOf(':') + 1).Trim());
        }

        private record LoopObject(int Pos, string FieldName, IEnumerator<DynValue> Enumerator);

    }
}
