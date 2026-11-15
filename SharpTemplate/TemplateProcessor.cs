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
        private readonly Script _script;
        private readonly StringBuilder sb = new StringBuilder();
        public TemplateProcessor(string template)
        {
            _template = template;
            _block = new CodeBlock(0) { Length = template.Length };
            _block.PreProcessing(template);
            _script = new Script();
            _script.Globals["this"] = UserData.Create(new TemplateProcessorDocument(sb));
        }

        public string Processing()
        {
            StringBuilder sb = new();
            _block.Processing(sb, _template, _script);
            return sb.ToString();
        }

        private class CodeBlock(int start)
        {
            private static CodeBlockComparer _codeBlockComparer = new CodeBlockComparer();

            public readonly int Start = start;
            protected int ContentStart = start;
            public int Length;
            public readonly List<CodeBlock> Childrens = [];
            private BlockType Type = BlockType.None;

            public virtual void Processing(StringBuilder sb, string template, Script script)
            {
                if (Childrens.Count == 0)
                {
                    sb.Append(template.Substring(Start, Length));
                    return;
                }
                FillSpaces();
                foreach (CodeBlock c in Childrens)
                {
                    c.FillSpaces();
                    c.Processing(sb, template, script);
                }
            }

            private void FillSpaces()
            {
                if (Childrens.Count == 0) return;

                var tempChildrens = Childrens.OrderBy(t => t.Start).ToList();
                if (!tempChildrens.Any(t => t.Start == Start)) tempChildrens.Insert(0, new CodeBlock(ContentStart) { Length = 0 });

                var lengthAdjustment = 0;
                if (Type == BlockType.Loop) lengthAdjustment = -2;
                if (Type == BlockType.Condition) lengthAdjustment = -2;

                foreach (CodeBlock c in tempChildrens)
                {
                    var cend = c.Start + c.Length;
                    if (cend == Start + Length + lengthAdjustment) break;
                    if (tempChildrens.Any(t => t.Start == cend && t.Length != 0)) continue;
                    var tmp = new CodeBlock(cend);
                    var next = (tempChildrens.Where(t => t.Start > c.Start).OrderBy(t => Start).FirstOrDefault()?.Start) ?? (Start + Length + lengthAdjustment);
                    tmp.Length = next - tmp.Start;
                    Childrens.Add(tmp);
                }
                Childrens.Sort(_codeBlockComparer);
            }

            public void PreProcessing(string template)
            {
                Stack<CodeBlock> stack = new Stack<CodeBlock>();
                for (int i = Start; i < Start + Length; i++)
                {
                    if (template[i] == '<' && template[i + 1] == '?')
                    {
                        i += 2;
                        if (template[i] == 'e') //else 
                        {
                            if (template.Substring(i).StartsWith("else>"))
                            {
                                var block = stack.Peek();
                                if (block is CodeBlockCondition cblock)
                                {
                                    i += 4;
                                    cblock.ElsePos = i;
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }
                            continue;
                        }

                        if (template[i] == '=' || template[i] == '#')
                        {
                            stack.Push(new CodeBlockLua(i - 2) { Type = (BlockType)template[i] });
                            continue;
                        }

                        if (template[i] == '@' || template[i] == '?')
                        {
                            var bStart = template.IndexOf('(', i) + 1;
                            var bEnd = template.IndexOf(')', bStart);
                            var block = template.Substring(bStart, bEnd - bStart);
                            if (template[i] == '@')
                                stack.Push(new CodeBlockLoop(i - 2) { ContentStart = bEnd + 1, Type = (BlockType)template[i], Code = block });
                            else
                                stack.Push(new CodeBlockCondition(i - 2) { ContentStart = bEnd + 1, Type = (BlockType)template[i], Code = block });
                            i = bEnd;
                        }
                        continue;
                    }

                    if (template[i] == '?' && template[i + 1] == '>')
                    {
                        if (stack.Count == 0) throw new Exception();

                        var block = stack.Pop();
                        i += 2;
                        block.Length = i - block.Start;
                        if (stack.Count == 0)
                            Childrens.Add(block);
                        else
                            stack.Peek().Childrens.Add(block);
                    }
                }
            }

            private enum BlockType : int
            {
                None = 0,
                Expression = '=',
                Code = '#',
                Loop = '@',
                Condition = '?'
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
                    var tmp = template.Substring(Start + 3, Length - 5);
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
                public int? ElsePos;
            }

            private class CodeBlockComparer : IComparer<CodeBlock>
            {
                public int Compare(CodeBlock? x, CodeBlock? y)
                {
                    if (x == null || y == null) return 0;
                    if (x == null) return -1;
                    if (y == null) return 1;
                    return x.Start - y.Start;
                }
            }
        }

    }
}
