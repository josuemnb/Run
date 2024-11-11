using ISIExtensions;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Run {

    public struct ReplaceToken {
        public string Format;
        public object[] Args;
    }
    public class Replacer(Builder builder) {
        public Builder Builder = builder;
        public Dictionary<string, ReplaceToken> TokenAnnotations = new(0);

        public void Replace() {
            //RegisterAnnotations();
            //Replace(Builder.Program);
            //ReplaceAll();
        }

        public static bool Self(AST ast, Expression by) {
            switch (ast.Parent) {
                case For @for:
                    if (@for.Start == ast) {
                        @for.Start = by;
                        return true;
                    }
                    if (@for.Condition == ast) {
                        @for.Condition = by;
                        return true;
                    }
                    if (@for.Step == ast) {
                        @for.Step = by;
                        return true;
                    }
                    return false;
                case If @if:
                    if (@if.Condition == ast) {
                        @if.Condition = by;
                        return true;
                    }
                    if (@if.Children.IndexOf(ast) is int ix && ix > -1) {
                        @if.Children[ix] = by;
                        return true;
                    }
                    return false;
                case CallExpression call:
                    if (call.Arguments.IndexOf(ast as Expression) is int cx && cx > -1) {
                        call.Arguments[cx] = by;
                        return true;
                    }
                    return false;
                case Block block:
                    var idx = block.Children.IndexOf(ast);
                    if (idx > -1) {
                        block.Children[idx] = by;
                        return true;
                    }
                    return false;
                case Var v:
                    v.Initializer = by;
                    return true;
                case BinaryExpression bin:
                    if (bin.Left == ast) {
                        bin.Left = by;
                    } else {
                        bin.Right = by;
                    }
                    return true;
                case ContentExpression content:
                    content.Content = by;
                    return true;
                default:
                    break;
            }
            return false;
        }

        public static bool Operator(BinaryExpression binary, Builder builder) {
            var type = binary.Left.Type;
            if (type.HasOperators == false) return false;
            for (int i = 0; i < type.Children.Count; i++) {
                var child = type.Children[i];
                if (child is Operator op && op.Token.Type == binary.Token.Type) {
                    if (op.Parameters.Children.Count != 1) {
                        binary.Program.AddError(binary.Token, Error.OnlyOneParameterAllowedInOperator);
                        break;
                    }
                    if (op.Parameters.Children[0] is Parameter p && p.Type != null && Validator.AreCompatible(p, binary.Right) == false) {
                        binary.Program.AddError(binary.Token, Error.IncompatibleType);
                        break;
                    }
                    //new Validator(builder).Validate(op);
                    var getter = new CallExpression(binary.Parent, false) {
                        Caller = binary.Left,
                        Type = op.Type,
                        Function = op,
                        Real = op.Real,
                        Arguments = [binary.Right],
                    };
                    //Counter.Count(op);
                    if (Self(binary, getter)) {
                        return true;
                    }
                }
            }
            return false;
        }

        void ReplaceAll() {
            foreach (var id in Builder.Program.DeepFindChildrenInternal<IdentifierExpression>()) {
                Property(id);
            }
        }

        internal static void Property(IdentifierExpression id) {
            if (id.From is not Property property || property.SimpleKind != PropertyKind.None) {
                return;
            }
            var caller = (id.Parent as DotExpression)?.Left;
            if (caller == null) {
                caller = new ThisExpression(id.Parent) {
                    Token = id.Token,
                };
                caller.Token.Value = "this";
            }
            var assign = id.Parent as AssignExpression;
            Expression dot = id;
            if (assign == null && id.Parent is DotExpression d) {
                dot = d;
                assign = d.Parent as AssignExpression;
            }
            if (assign != null && assign.Left == dot) {
                if (property.Setter == null) {
                    id.Program.AddError(id.Token, Error.PropertyIsNotWritable);
                    return;
                }
                var getter = new CallExpression(assign.Parent, false) {
                    Caller = caller,
                    Function = property.Setter,
                    Real = property.Setter.Real,
                    Arguments = [assign.Right]
                };
                //Counter.Count(getter);
                Self(assign, getter);
            } else {
                if (property.Getter == null) {
                    id.Program.AddError(id.Token, Error.PropertyIsNoReadable);
                    return;
                }
                var getter = new CallExpression(id.Parent, false) {
                    Caller = caller,
                    Function = property.Getter,
                    Real = property.Getter.Real,
                    Type = property.Getter.Type,
                };
                //Counter.Count(getter);
                Self(caller is ThisExpression ? id : id.Parent, getter);
            }
        }

        internal static void Indexer(IndexerExpression expression, Indexer indexer) {
            if (expression.Parent is AssignExpression assign && assign.Left == expression) {
                //Counter.Count(indexer.Setter);
                var getter = new CallExpression(assign, false) {
                    Caller = expression.Left,
                    Function = indexer.Setter,
                    Real = indexer.Setter.Real,
                    Arguments = [expression.Right, assign.Right]
                };
                Self(assign, getter);
            } else {
                //Counter.Count(indexer.Getter);
                var getter = new CallExpression(expression.Parent, false) {
                    Caller = expression.Left,
                    Function = indexer.Getter,
                    Real = indexer.Getter.Real,
                    Type = indexer.Getter.Type,
                    Arguments = [expression.Right]
                };
                Self(expression.Left is BinaryExpression ? expression.Parent : expression, getter);
            }
        }

        void RegisterAnnotations() {
            foreach (var ast in Builder.Classes.Values) {
                if (ast.Annotations == null) continue;
                for (int i = 0; i < ast.Annotations.Count; i++) {
                    var ann = ast.Annotations[i];
                    if (ann.IsReplace == false) continue;
                    ast.Usage++;
                    var split = ann.Value.Split('=');
                    if (split.Length != 2) continue;
                    if (System.Enum.TryParse<TokenType>(split[0], out _) == false) {
                        Builder.Program.AddError(ann.Token, Error.UnknownTokenType);
                        continue;
                    }
                    var s = split[1].Trim();
                    ReplaceToken replacer = new();
                    if (s.Contains('.') == false) continue;
                    var format = new StringBuilder();
                    var args = new List<object>();
                    int l = 0;
                    int count = 0;
                    while (l < s.Length) {
                        if (s[l] == '.') {
                            if (char.IsLetter(s[l + 1])) {
                                l++;
                                int p = l;
                                while (char.IsLetter(s[l])) {
                                    l++;
                                }
                                var prop = s[p..l];
                                switch (prop) {
                                    case "Value":
                                        format.Append("\"{").Append(count++).Append("}\"");
                                        args.Add(ann.Token.Value);
                                        break;
                                    case "Length":
                                        format.Append('{').Append(count++).Append('}');
                                        args.Add(ann.Token.Length);
                                        break;
                                    default:
                                        if (ann.Token.GetType().GetProperty(prop) is var propInfo && propInfo != null) {
                                            format.Append('{').Append(count++).Append('}');
                                            args.Add(propInfo.GetValue(ann.Token));
                                        }
                                        break;
                                }
                            }
                            continue;
                        } else if (s[l] == '{' || s[l] == '}') {
                            format.Append(s[l]);
                        }
                        format.Append(s[l]);
                        l++;
                    }
                    replacer = new ReplaceToken() {
                        Format = format.ToString(),
                        Args = [.. args],
                    };
                    if (TokenAnnotations.TryAdd(split[0], replacer) == false) {
                        Builder.Program.AddError(ann.Token, Error.TokenAnnotationAlreadyExists);
                    }
                }
            }
        }

        void ApplyAnnotations(AST ast) {
            if (TokenAnnotations.Count == 0) return;
            if (ast.Token != null && ast.Token.Type != TokenType.NONE) {
                if (TokenAnnotations.TryGetValue(ast.Token.Type.ToString(), out var replacer)) {
                    ast.Token.Value = string.Format(replacer.Format, replacer.Args);
                }
            }
        }

        internal static NewExpression Generic(NewExpression n, Builder builder) {
            if (n.Generic == null) return n;

            var cls = n.FindParent<Class>();
            if (cls == null || cls.HasGenerics == false) {
                n.Program.AddError(n.Token, Error.InvalidExpression);
                return n;
            }
            var index = cls.Generics.IndexOf(n.Generic);
            if (index == -1) return n;
            if (builder.Find(cls.Generics[index].Real) is not Class type) {
                n.Program.AddError(n.Token, Error.InvalidExpression);
                return n;
            }
            n.Type = type;
            return n;
        }

        internal static NewExpression Generics(NewExpression n, Builder builder) {
            if (n.HasGenerics == false) return n;

            if (ValidateGenerics(n, builder) is not Class newType) return n;
            n.Type = newType;
            return n;
        }

        static string GetGenericRealName(AST ast, Builder builder) {
            if (ast.Generics.IsEmpty()) return null;

            var buff = new StringBuilder(ast.Token.Value);
            for (int i = 0; i < ast.Generics.Count; i++) {
                var generic = ast.Generics[i];
                if (generic.HasGenerics) {
                    var sub = ValidateGenerics(generic, builder);
                    if (sub == null) {
                        builder.Program.AddError(generic.Token, Error.UnknownType);
                        return null;
                    }
                    generic.Real = sub.Token.Value;
                    if (buff.Length > 0) buff.Append('_');
                    buff.Append(sub.Token.Value);
                } else {
                    var sub = builder.Find(generic.Token.Value);
                    if (sub == null) {
                        builder.Program.AddError(generic.Token, Error.UnknownType);
                        return null;
                    }
                    if (buff.Length > 0) buff.Append('_');
                    buff.Append(sub.Token.Value);
                    generic.Real = sub.Token.Value;
                }
            }
            return buff.ToString();
        }

        public static Class ValidateGenerics(AST ast, Builder builder) {
            var newName = GetGenericRealName(ast, builder);
            if (newName == null) return null;

            if (builder.Find(newName) is Class c) {
                return c;
            }
            if (builder.Find(ast.Token.Value) is not Class based) {
                builder.Program.AddError(ast.Token, Error.UnknownType);
                return null;
            }
            return ConvertGeneric(ast, based, newName, builder);
        }

        static Class ConvertGeneric(AST ast, Class based, string newName, Builder builder) {
            var clone = based.Clone();
            clone.Validated = false;
            clone.Children = [];
            clone.BaseType = based;
            clone.BaseToken = based;
            clone.Token = ast.Token.Clone();
            clone.Token.Value = newName;
            clone.Generics = null;
            clone.Real = "_" + newName;
            ReplaceGenericsMembers(clone, ast, based, builder);
            builder.Classes.Add(clone.Token.Value, clone);
            builder.Program.Validator.Validate(clone);
            return clone;
        }

        private static void ReplaceGenericsMembers(Class clone, AST ast, Class based, Builder builder) {
            foreach (var child in based.Children) {
                switch (child) {
                    case Var v when v.Generic != null:
                        if (ReplaceGenericsInVar(v, ast, based, builder) is Var cloneVar) {
                            clone.Add(cloneVar);
                        }
                        break;
                    case Function f when f.HasGenerics:
                        if (ReplaceGenericFunction(f, ast, based, builder) is Function cloneFunc) {
                            clone.Add(cloneFunc);
                        }
                        break;
                }
            }
        }

        static Function ReplaceGenericFunction(Function func, AST ast, Class based, Builder builder) {
            var clone = func.Clone();
            clone.Token = func.Token.Clone();
            clone.Validated = false;
            clone.Children = [];
            clone.Generics = null;
            clone.HasGeneric = false;
            CloneChildren(func, clone, ast, based, builder);
            if (func.Parameters != null) {
                clone.Parameters = clone.Children[0] as Block;
            }
            clone.Real = null;
            builder.SetRealName(clone);
            if (builder.Functions.ContainsKey(clone.Real)) {
                clone.Real += "_" + clone.Token.ID;
            }
            builder.RegisterFunction(clone);
            return clone;
        }

        static void CloneChildren(Block source, Block destination, AST ast, Class based, Builder builder) {
            foreach (var child in source.Children) {
                switch (child) {
                    case Var v: {
                            if (v.HasGenerics == false || ReplaceGenericsInVar(v, ast, based, builder) is not Var clone) {
                                clone = v.Clone();
                                clone.Validated = false;
                            }
                            destination.Add(clone);
                        }
                        break;
                    case Block block: {
                            var clone = block.Clone();
                            clone.Validated = false;
                            clone.Children = [];
                            destination.Add(clone);
                            CloneChildren(block, clone, ast, based, builder);
                        }
                        break;
                    case Expression exp: {
                            var clone = ExpressionHelper.Clone(exp);
                            clone.Validated = false;
                            destination.Add(clone);
                            ReplaceGenericsInExpression(clone, ast, based, builder);
                        }
                        break;
                    default: {
                            var clone = child.Clone();
                            clone.Validated = false;
                            destination.Add(clone);
                        }
                        break;
                }
            }
        }

        private static void ReplaceGenericsInExpression(Expression clone, AST ast, Class based, Builder builder) {
            foreach (var ce in ExpressionHelper.FindChildren<ValueType>(clone)) {
                if (ce.Generic == null) continue;

                var index = based.Generics.IndexOf(ce.Generic);
                if (index == -1) continue;
                if (builder.Find(ast.Generics[index].Real) is not Class type) continue;

                switch (ce) {
                    case SizeOf sz:
                        sz.Type = type;
                        sz.Generic = null;
                        sz.Validated = false;
                        sz.Content.Real = type.Real;
                        sz.Content.Token = type.Token;
                        break;
                    case TypeExpression te:
                        te.Real = type.Real;
                        te.Token = type.Token;
                        te.Type = type;
                        te.Generic = null;
                        te.Validated = false;
                        break;
                    case AsExpression a:
                        a.Validated = false;
                        break;
                }
            }
        }

        static Var ReplaceGenericsInVar(Var v, AST ast, Class based, Builder builder) {
            if (v.HasGenerics == false) return null;

            var clone = v.Clone();
            clone.Generic = null;
            clone.Validated = false;
            clone.InitializerHasGeneric = false;
            clone.Generics = null;

            if (v.Generic != null) {
                var index = based.Generics.IndexOf(v.Generic);
                if (index == -1) return null;
                if (builder.Find(ast.Generics[index].Real) is not Class type) return null;

                clone.Type = type;
            }
            if (v.InitializerHasGeneric) {
                clone.Initializer = ExpressionHelper.Clone(v.Initializer);
                clone.Initializer.Validated = false;
                ReplaceGenericsInExpression(clone.Initializer, ast, based, builder);
            }

            foreach (var exp in based.FindChildren<Expression>(true)) {
                foreach (var child in ExpressionHelper.FindChildren<IdentifierExpression>(exp)) {
                    if (child.Token.Value == v.Token.Value) {
                        child.Type = clone.Type;
                        child.From = clone;
                    }
                }
            }

            return clone;
        }
    }
}