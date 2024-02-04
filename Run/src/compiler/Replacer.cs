using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            ReplaceAll();
        }

        void ReplaceAll() {
            foreach (var id in Builder.Program.DeepFindChildrenInternal<IdentifierExpression>()) {
                if (id.From is Property property && property.SimpleKind == Property.PropertyKind.None) {
                    var assign = Expression.FindAssignment(id);
                    if (assign != null) {
                        var caller = id.Parent as DotExpression;
                        property.Getter.Usage = 1;
                        var getter = new CallExpression(assign, false) {
                            Caller = caller?.Left,
                            Function = property.Getter,
                            Real = property.Getter.Real,
                        };
                        if (assign is AssignExpression a) a.Right = getter;
                        if (assign is Var v) v.Initializer = getter;
                    }
                }
            }
            foreach (var binary in Builder.Program.DeepFindChildrenInternal<BinaryExpression>()) {
                if (binary.Parent is Block b) {
                    var index = b.Children.IndexOf(binary);
                    if (index == -1) continue;
                }
                var type = binary.Left.Type;
                if (type == null || type.Children.Count == 0 || type.HasOperators == false) continue;
                for (int i = 0; i < type.Children.Count; i++) {
                    var child = type.Children[i];
                    if (child is Operator op && op.Token.Type == binary.Token.Type) {
                        if (op.Parameters.Children.Count != 1) {
                            Builder.Program.AddError(binary.Token, Error.OnlyOneParameterAllowedInOperator);
                            break;
                        }
                        if (op.Parameters.Children[0] is Parameter p && p.Type != null && Validator.AreCompatible(Builder, p, binary.Right) == false) {
                            Builder.Program.AddError(binary.Token, Error.IncompatibleType);
                            break;
                        }
                        op.Usage = 1;
                        var getter = new CallExpression(binary.Parent, false) {
                            Caller = binary.Left,
                            Function = op,
                            Real = op.Real,
                            Arguments = new List<Expression> { binary.Right, },
                        };
                        if (binary.Parent is Block block) {
                            block.Children[block.Children.IndexOf(binary)] = getter;
                            break;
                        } else if (binary.Parent is BinaryExpression bin) {
                            if (bin.Left == binary) bin.Left = getter;
                            else bin.Right = getter;
                        }
                        //return new Caller() {
                        //    Token = bin.Token,
                        //    Parent = bin.Parent,
                        //    Function = op,
                        //    Type = op.Type,
                        //    Real = op.Real,
                        //    Parameters = new() { bin.Left, bin.Right },
                        //};
                    }
                }
            }
        }

        void RegisterAnnotations() {
            foreach (var ast in Builder.Classes.Values) {
                if (ast.Annotations != null) {
                    for (int i = 0; i < ast.Annotations.Count; i++) {
                        var ann = ast.Annotations[i];
                        if (ann.IsReplace) {
                            ast.Usage++;
                            var split = ann.Value.Split('=');
                            if (split.Length != 2) continue;
                            if (System.Enum.TryParse<TokenType>(split[0], out _) == false) {
                                Builder.Program.AddError(ann.Token, Error.UnknownTokenType);
                                continue;
                            }
                            var s = split[1].Trim();
                            ReplaceToken replacer = new();
                            if (s.Contains('.')) {
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
                            }
                            if (TokenAnnotations.TryAdd(split[0], replacer) == false) {
                                Builder.Program.AddError(ann.Token, Error.TokenAnnotationAlreadyExists);
                            }
                        }
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

        Block Replace(Block block) {
            if (block.Replaced) return block;
            block.Replaced = true;
            for (int i = 0; i < block.Children.Count; i++) {
                block.Children[i] = Replace(block.Children[i]);
            }
            return block;
        }

        AST Replace(AST ast) {
            //ApplyAnnotations(ast);
            switch (ast) {
                case For @for: return Replace(@for);
                case If @if: return Replace(@if);
                case Return @return: return Replace(@return);
                case ThisExpression t: return Replace(t);
                case Property property: return Replace(property);
                case Var var: return Replace(var);
                case Block block: return Replace(block);
                case CallExpression call: return Replace(call);
                case TernaryExpression ter: return Replace(ter);
                case ParentesesExpression p: return Replace(p);
                case DotExpression dot: return Replace(dot);
                case CastExpression cast: return Replace(cast);
                //case SizeOf s: return Replace(s);
                case TypeOf t: return Replace(t);
                case NewExpression n: return Replace(n);
                case IdentifierExpression id: return Replace(id);
                case BinaryExpression bin: return Replace(bin);
                case DeclaredType dc: return Replace(dc);
                case ContentExpression expr: expr.Content = (Expression)Replace(expr.Content); return expr;
            }
            return ast;
        }

        DeclaredType Replace(DeclaredType dc) {
            if (dc.Caller != null) {
                dc.Caller = Replace(dc.Caller);
            }
            return dc;
        }

        For Replace(For @for) {
            if (@for.Condition != null) {
                @for.Condition = (Expression)Replace(@for.Condition);
            }
            if (@for.Step != null) {
                @for.Step = (Expression)Replace(@for.Step);
            }
            if (@for.Start != null) {
                @for.Start = Replace(@for.Start);
            }
            Replace(@for as Block);
            return @for;
        }

        If Replace(If @if) {
            //may is else
            if (@if.Condition != null) {
                @if.Condition = (Expression)Replace(@if.Condition);
            }
            Replace(@if as Block);
            return @if;
        }

        Return Replace(Return @return) {
            if (@return.Expression != null) {
                @return.Expression = (Expression)Replace(@return.Expression);
            }
            return @return;
        }

        static ThisExpression Replace(ThisExpression t) {
            return t;
        }
        Property Replace(Property property) {
            if (property.Initializer != null) {
                property.Initializer = (Expression)Replace(property.Initializer);
            }
            if (property.Getter != null) {
                property.Getter = Replace(property.Getter) as Function;
            }
            if (property.Setter != null) {
                property.Setter = Replace(property.Setter) as Function;
            }
            return property;
        }
        Var Replace(Var var) {
            if (var.Initializer != null) {
                var.Initializer = (Expression)Replace(var.Initializer);
            }
            return var;
        }
        CallExpression Replace(CallExpression call) {
            if (call.Arguments != null) {
                for (int i = 0; i < call.Arguments.Count; i++) {
                    call.Arguments[i] = (Expression)Replace(call.Arguments[i]);
                }
            }
            return call;
        }
        TernaryExpression Replace(TernaryExpression ter) {
            ter.Condition = (Expression)Replace(ter.Condition);
            ter.True = (Expression)Replace(ter.True);
            ter.False = (Expression)Replace(ter.False);
            return ter;
        }
        ParentesesExpression Replace(ParentesesExpression p) {
            p.Content = (Expression)Replace(p.Content);
            return p;
        }

        static Expression Replace(IdentifierExpression id) {
            if (id.Virtual) return id;
            if (id.From is Property property && property.SimpleKind == Property.PropertyKind.None) {
                var assign = Expression.FindParent<AssignExpression>(id);
                if (assign != null) {
                    property.Getter.Usage = 1;
                    var getter = new CallExpression(assign, false) {
                        Function = property.Getter,
                        Real = property.Getter.Real,
                    };
                    assign.Right = getter;
                    return getter;
                }
            }
            return id;
        }
        AST Replace(DotExpression dot) {
            dot.Left = (Expression)Replace(dot.Left);
            dot.Right = (Expression)Replace(dot.Right);
            if (dot.Right is PropertySetter caller && caller.Back > 0) {
                caller.Back--;
                caller.This = dot.Left;
                return caller;
            }
            return dot;
        }
        CastExpression Replace(CastExpression cast) {
            cast.Left = (Expression)Replace(cast.Left);
            cast.Right = (Expression)Replace(cast.Right);
            return cast;
        }
        SizeOf Replace(SizeOf sizeOf) {
            sizeOf.Content = (Expression)Replace(sizeOf.Content);
            return sizeOf;
        }
        TypeOf Replace(TypeOf typeOf) {
            typeOf.Content = (Expression)Replace(typeOf.Content);
            return typeOf;
        }
        NewExpression Replace(NewExpression @new) {
            //@new.Expression.Content = Replace(@new.Expression.Content);
            //@new.Caller = (Caller)Replace(@new.Caller);
            //@new.Calling = Replace(@new.Calling);
            @new.Content = (Expression)Replace(@new.Content);
            return @new;
        }
        AST Replace(BinaryExpression bin) {
            bin.Left = (Expression)Replace(bin.Left);
            bin.Right = (Expression)Replace(bin.Right);
            if (bin.Token == null) {
                return bin;
            }
            var cls = (bin.Left as ValueType)?.Type;
            if (cls == null) return bin;
            for (int i = 0; i < cls.Children.Count; i++) {
                var child = cls.Children[i];
                if (child is Operator op && op.Token.Type == bin.Token.Type) {
                    if (op.Parameters.Children.Count != 1) {
                        Builder.Program.AddError(bin.Token, Error.OnlyOneParameterAllowedInOperator);
                        break;
                    }
                    if (op.Parameters.Children[0] is Parameter p && p.Type != null && Validator.AreCompatible(Builder, p, bin.Right as ValueType) == false) {
                        Builder.Program.AddError(bin.Token, Error.IncompatibleType);
                        break;
                    }
                    //return new Caller() {
                    //    Token = bin.Token,
                    //    Parent = bin.Parent,
                    //    Function = op,
                    //    Type = op.Type,
                    //    Real = op.Real,
                    //    Parameters = new() { bin.Left, bin.Right },
                    //};
                }
            }
            if (bin.Left is PropertySetter caller && caller.Back > 0) {
                caller.Back--;
                return caller;
            }
            return bin;
        }
    }
}