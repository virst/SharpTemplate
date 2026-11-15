using MoonSharp.Interpreter;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace SharpTemplate
{
    public class TemplateProcessorDocument(StringBuilder sb)
    {
        private readonly StringBuilder sb = sb;

        static TemplateProcessorDocument()
        {
            UserData.RegisterType<TemplateProcessorDocument>();
        }

        public void Echo(object o)
        {
            sb.Append(o);
        }
    }

    public class TemplateProcessor
    {
        private readonly string _template;
        private readonly CodeBlock _block;
        protected readonly Script _script;

        public TemplateProcessor(string template)
        {
            _template = template;
            _block = new CodeBlock(0) { Length = template.Length };
            _block.PreProcessing(template);
            _script = new Script();
        }

        public string Processing()
        {
            StringBuilder sb = new();

            _script.Globals["this"] = UserData.Create(new TemplateProcessorDocument(sb));
            _block.Processing(sb, _template, _script);
            return sb.ToString();
        }

        private class CodeBlock(int start)
        {
            public readonly int Start = start;
            protected int ContentStart = start;
            public int Length;
            public readonly List<CodeBlock> Childrens = [];
            private BlockType Type = BlockType.None;

            public virtual void Processing(StringBuilder sb, string template, Script script)
            {
                if (Type == BlockType.Else) return;

                if (Childrens.Count == 0)
                {
                    sb.Append(template.Substring(Start, Length));
                    return;
                }
                foreach (CodeBlock c in Childrens)
                {
                    c.Processing(sb, template, script);
                }
            }

            public void PreProcessing(string template)
            {
                CodeBlock currentBlock = new(ContentStart);
                var len = Length;
                if (Type != BlockType.None) len -= 2;
                var end = Start + len;
                var deep = 0;
                for (int i = ContentStart; i <= end; i++)
                {
                    if (i == end)
                    {
                        currentBlock.Length = i - currentBlock.Start;
                        if (currentBlock.Length != 0)
                            Childrens.Add(currentBlock);
                        break;
                    }

                    if (template[i] == '<' && template[i + 1] == '?')
                    {
                        if (template[i + 2] == 'e') //else 
                        {
                            i += 2;
                            if (deep == 0 && template.Substring(i).StartsWith("else>"))
                            {
                                currentBlock.Length = i - currentBlock.Start - 2;
                                if (currentBlock.Length != 0)
                                    Childrens.Add(currentBlock);
                                Childrens.Add(new CodeBlock(i) { Type = (BlockType)template[i], Length = 4 });
                                currentBlock = new CodeBlock(i + 5);
                            }
                            i += 5;
                            continue;
                        }

                        deep++;
                        if (deep != 1) continue;
                        if (i != currentBlock.Start)
                        {
                            currentBlock.Length = i - currentBlock.Start;
                            if (currentBlock.Length != 0)
                                Childrens.Add(currentBlock);
                        }

                        i += 2;

                        if (template[i] == '=' || template[i] == '#')
                        {
                            currentBlock = new CodeBlockLua(i) { Type = (BlockType)template[i] };
                            continue;
                        }

                        if (template[i] == '@' || template[i] == '?')
                        {
                            var bStart = template.IndexOf('(', i) + 1;
                            var bEnd = template.IndexOf(')', bStart);
                            var block = template.Substring(bStart, bEnd - bStart);
                            if (template[i] == '@')
                                currentBlock = new CodeBlockLoop(i) { ContentStart = bEnd + 1, Type = (BlockType)template[i], Code = block };
                            else
                                currentBlock = new CodeBlockCondition(i) { ContentStart = bEnd + 1, Type = (BlockType)template[i], Code = block };
                            i = bEnd;
                            continue;
                        }
                        throw new Exception();
                    }

                    if (template[i] == '?' && template[i + 1] == '>')
                    {
                        deep--;
                        if (deep != 0) continue;
                        if (currentBlock.Type == BlockType.None) throw new Exception();
                        i += 2;
                        currentBlock.Length = i - currentBlock.Start;
                        if (currentBlock.Length != 0)
                            Childrens.Add(currentBlock);
                        currentBlock = new CodeBlock(i);
                    }
                }

                foreach (var c in Childrens)
                {
                    if (c.Type == BlockType.None) continue;
                    c.PreProcessing(template);
                }
            }

            private enum BlockType : int
            {
                None = 0,
                Expression = '=',
                Code = '#',
                Loop = '@',
                Condition = '?',
                Else = 'e'
            }

            private static (string metod, string param) SplitMetod(string s)
            {
                return (metod: s.Substring(0, s.IndexOf(':')).Trim(),
                    param: s.Substring(s.IndexOf(':') + 1).Trim());
            }

            private class CodeBlockLua(int start) : CodeBlock(start)
            {
                public override void Processing(StringBuilder sb, string template, Script script)
                {
                    var tmp = template.Substring(Start + 1, Length - 3);
                    if (Type == BlockType.Expression)
                    {
                        var result = script.DoString("return " + tmp);
                        sb.Append(result);
                    }
                    if (Type == BlockType.Code)
                    {
                        script.DoString(tmp);
                    }
                }
            }
            private class CodeBlockLoop(int start) : CodeBlockLua(start)
            {
                public string? Code;
                public override void Processing(StringBuilder sb, string template, Script script)
                {
                    if (Code is null) throw new Exception();

                    var metod = SplitMetod(Code);
                    var result = script.DoString("return " + metod.param);
                    foreach (var v in result.Table.Values)
                    {
                        script.Globals[metod.metod] = v;
                        foreach (var c in Childrens)
                            c.Processing(sb, template, script);
                    }
                }
            }

            private class CodeBlockCondition(int start) : CodeBlockLoop(start)
            {
                public override void Processing(StringBuilder sb, string template, Script script)
                {
                    var ll = Childrens.Select(t => template.Substring(t.Start, t.Length)).ToArray();
                    var result = script.DoString("return " + Code);
                    var ElsePos = Childrens.FirstOrDefault(t => t.Type == BlockType.Else)?.Start;
                    if (result.Boolean)
                    {
                        foreach (var c in Childrens.Where(t => ElsePos is null || t.Start < ElsePos))
                            c.Processing(sb, template, script);
                    }
                    else
                    {
                        foreach (var c in Childrens.Where(t => ElsePos is not null && t.Start > ElsePos))
                            c.Processing(sb, template, script);
                    }
                }
            }
        }
    }
}
