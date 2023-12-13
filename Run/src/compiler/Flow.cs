using System.Diagnostics;
using System.IO;

namespace Run {

    public class Label : AST {
        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write(Token.Value);
            writer.WriteLine(':');
        }
    }

    public class Goto : AST {
        public override void Parse() {
            if (GetName(out Token) == false) {
                return;
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("goto ");
            writer.Write(Token.Value);
            writer.WriteLine(';');
        }
    }
    public class Else : If {
        public override void Parse() {
            if (Scanner.Expect('{')) {
                ParseBlock();
                return;
            }
            if (Scanner.Expect("if")) {
                base.Parse();
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("else ");
            if (Condition != null) {
                base.Save(writer, builder);
            } else {
                writer.WriteLine('{');
                SaveBlock(writer, builder);
                writer.WriteLine('}');
            }
        }
    }

    public class If : Block {
        public ExpressionV2 Condition;

        public override void Parse() {
            Condition = new ExpressionV2();
            Condition.SetParent(this);
            Condition.Parse();
            //if (Scanner.Expect("=>")) {
            if (Scanner.Current.Type == TokenType.ARROW) {
                ParseBlock(true);
                return;
            }
            //if (Scanner.Expect('{') == false) {
            if (Scanner.Current.Type != TokenType.OPEN_BLOCK) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
            base.Parse();
            if (Scanner.Expect("else")) {
                (Parent as Block).Add<Else>().Parse();
            }
        }

        protected void SaveBlock(TextWriter writer, Builder builder) => base.Save(writer, builder);

        //protected new void ParseBlock() => base.Parse();

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("if(");
            Condition.Save(writer, builder);
            writer.WriteLine(") {");
            SaveBlock(writer, builder);
            writer.WriteLine("}");
        }

        public override void Print() {
            AST.Print(this);
            Condition?.Print();
            foreach (var child in Children) {
                child.Print();
            }

        }
    }
    public class For : Block {
        public AST Start;
        public ExpressionV2 Condition;
        public ExpressionV2 Step;
        int Stage = -1;

        public override void Parse() {
            if (Scanner.Current.Line == 13) ;
        again:
            if (Scanner.Expect("=>")) {
                goto once;
            }
            if (Scanner.Expect('{')) {
                goto end;
            }
            if (Stage == -1) Stage = 0;
            if (Scanner.Expect(';')) {
                Stage++;
                goto again;
            }
            AST current = null;
            switch (Stage) {
                case 0:
                    var peek = Scanner.Test();
                    if (peek.Value == "var") {
                        Scanner.Scan();
                        current = Start = new Var();
                    } else {
                        current = Start = new ExpressionV2();
                    }
                    break;
                case 1:
                    current = Condition = new ExpressionV2();
                    break;
                case 2:
                    current = Step = new ExpressionV2();
                    break;
            }
            current?.SetParent(this);
            current?.Parse();
            Scanner.RollBack();
            if (current is ExpressionV2 exp && exp.HasError) {
                return;
            }
            if (Stage < 2) {
                goto again;
            }
            if (Scanner.Expect("=>")) {
                goto once;
            }
            if (Scanner.Expect('{') == false) {
                Program.AddError(Scanner.Current, Error.ExpectingBeginOfBlock);
                return;
            }
        end:
            base.Parse();
            return;
        once:
            ParseBlock(true);
        }

        public override void Save(TextWriter writer, Builder builder) {
            switch (Stage) {
                case -1:
                    writer.Write("while(1");
                    break;
                case 0 when Start is ExpressionV2 condition:
                    writer.Write("while(");
                    condition.Save(writer, builder);
                    break;
                case 0 when Start is Var var:
                    writer.Write("for(");
                    var.Save(writer, builder);
                    writer.Write(";;");
                    writer.Write(var.Token.Value);
                    writer.Write("++");
                    break;
                default:
                    writer.Write("for(");
                    Start?.Save(writer, builder);
                    writer.Write(';');
                    Condition?.Save(writer, builder);
                    writer.Write(';');
                    Step?.Save(writer, builder);
                    break;
            }
            if (Children.Count > 0) {
                writer.WriteLine(") {");
                base.Save(writer, builder);
                writer.WriteLine("}");
            } else {
                writer.WriteLine(");");
            }
        }

    }
    public class Break : AST {
        public override void Save(TextWriter writer, Builder builder) {
            writer.WriteLine("break;");
        }
    }
    public class Continue : AST {
        public override void Save(TextWriter writer, Builder builder) {
            writer.WriteLine("continue;");
        }
    }

    public class Delete : ValueType {
        public Block Block;
        public override void Parse() {
            Block = new Block();
            Block.SetParent(this);
            bool parenteses = Scanner.Expect('(');
        again:
            if (GetName(out Token name) == false) {
                Scanner.SkipLine();
                return;
            }
            Block.Add<Identifier>().Token = name;
            if (Scanner.Expect(',')) {
                goto again;
            }
            if (parenteses) {
                if (Scanner.Expect(')') == false) {
                    Program.AddError(Scanner.Current, Error.ExpectingCloseParenteses);
                }
            } else if (Scanner.IsEOL() == false) {
                Program.AddError(Scanner.Current, Error.ExpectingEndOfLine);
            }
        }

        public override void Save(TextWriter writer, Builder builder) {
            for (int i = 0; i < Block.Children.Count; i++) {
                var child = Block.Children[i];
                if (child is Identifier id && id.Type is Class cls && cls.Dispose != null) {
                    writer.Write(cls.Dispose.Real);
                    writer.Write("(");
                    writer.Write(id.Token.Value);
                    writer.WriteLine(");");
                }
                writer.Write("DELETE(");
                child.Save(writer, builder);
                writer.Write(")");
                if (i < Block.Children.Count - 1) {
                    writer.WriteLine(';');
                }
            }
        }
    }

    public class Null : ValueType {
        public Null() {
            IsNull = true;
        }

        public override void Save(TextWriter writer, Builder builder) {
            writer.Write("NULL");
        }
    }
}
