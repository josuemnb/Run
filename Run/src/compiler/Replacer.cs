using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Run {

    public struct ReplaceToken {
        public string Format;
        public object[] Args;
    }
    public class Replacer {
        public Builder Builder;
        public Dictionary<string, ReplaceToken> TokenAnnotations = new(0);
        public Replacer(Builder builder) {
            Builder = builder;
        }
        public void Replace() {
            //RegisterAnnotations();
            Replace(Builder.Program);
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
                                                    format.Append("{").Append(count++).Append("}");
                                                    args.Add(ann.Token.Length);
                                                    break;
                                                default:
                                                    if (ann.Token.GetType().GetProperty(prop) is var propInfo && propInfo != null) {
                                                        format.Append("{").Append(count++).Append("}");
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
                                    Args = args.ToArray(),
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
                case This t: return Replace(t);
                case Property property: return Replace(property);
                case Var var: return Replace(var);
                case Block block: return Replace(block);
                case Expression expr: expr.Result = Replace(expr.Result); return expr;
                case Caller call: return Replace(call);
                case Ternary ter: return Replace(ter);
                case Parenteses p: return Replace(p);
                case MemberAccess dot: return Replace(dot);
                case Cast cast: return Replace(cast);
                case SizeOf s: return Replace(s);
                case TypeOf t: return Replace(t);
                case New n: return Replace(n);
                case Binary bin: return Replace(bin);
                case Identifier id: return Replace(id);
                case DeclaredType dc: return Replace(dc);
            }
            return ast;
        }

        DeclaredType Replace(DeclaredType dc) {
            if (dc.Caller != null) {
                dc.Caller = Replace(dc.Caller);
            }
            return dc;
        }

        AST Replace(For @for) {
            if (@for.Condition != null) {
                @for.Condition.Result = Replace(@for.Condition.Result);
            }
            if (@for.Step != null) {
                @for.Step.Result = Replace(@for.Step.Result);
            }
            if (@for.Start != null) {
                @for.Start = Replace(@for.Start);
            }
            Replace(@for as Block);
            return @for;
        }

        AST Replace(If @if) {
            //may is else
            if (@if.Condition != null) {
                @if.Condition.Result = Replace(@if.Condition.Result);
            }
            Replace(@if as Block);
            return @if;
        }

        AST Replace(Return @return) {
            if (@return.Expression != null) {
                @return.Expression.Result = Replace(@return.Expression.Result);
            }
            return @return;
        }
        AST Replace(This t) {
            return t;
        }
        AST Replace(Property property) {
            if (property.Initializer != null) {
                property.Initializer.Result = Replace(property.Initializer.Result);
            }
            if (property.Getter != null) {
                property.Getter = Replace(property.Getter) as Function;
            }
            if (property.Setter != null) {
                property.Setter = Replace(property.Setter) as Function;
            }
            return property;
        }
        AST Replace(Var var) {
            if (var.Initializer != null) {
                var.Initializer.Result = Replace(var.Initializer.Result);
            }
            return var;
        }
        AST Replace(Caller call) {
            if (call.Parameters != null) {
                for (int i = 0; i < call.Parameters.Count; i++) {
                    call.Parameters[i] = Replace(call.Parameters[i]);
                }
            }
            return call;
        }
        AST Replace(Ternary ter) {
            ter.Condition = Replace(ter.Condition);
            ter.IsTrue.Result = Replace(ter.IsTrue.Result);
            ter.IsFalse.Result = Replace(ter.IsFalse.Result);
            return ter;
        }
        AST Replace(Parenteses p) {
            p.Expression = Replace(p.Expression);
            return p;
        }
        AST Replace(Identifier id) {
            if (id.Virtual) return id;
            if (id.From is Property property && property.SimpleKind == Property.PropertyKind.None) {
                int back = 1;
                var parent = id.Parent as Binary;
                if (parent == null && id.Parent != null) {
                    back++;
                    parent = id.Parent.Parent as Binary;
                }
                if (parent != null && (parent.Left == id || parent.Left == id.Parent) && parent.Token.Type == TokenType.ASSIGN) {
                    return new PropertySetter {
                        Back = back,
                        Function = property.Setter,
                        Parent = id.Parent,
                        From = property,
                        Real = property.Setter.Real,
                        Parameters = new() { parent.Right },
                    };
                }
                return new Caller {
                    From = property,
                    Parent = id.Parent,
                    Function = property.Getter,
                    Real = property.Getter.Real,
                };
            }
            return id;
        }
        AST Replace(MemberAccess memberAccess) {
            memberAccess.Left = Replace(memberAccess.Left);
            memberAccess.Right = Replace(memberAccess.Right);
            if (memberAccess.Right is PropertySetter caller && caller.Back > 0) {
                caller.Back--;
                caller.This = memberAccess.Left;
                return caller;
            }
            return memberAccess;
        }
        AST Replace(Cast cast) {
            cast.Expression.Result = Replace(cast.Expression.Result);
            return cast;
        }
        AST Replace(SizeOf sizeOf) {
            sizeOf.Expression.Result = Replace(sizeOf.Expression.Result);
            return sizeOf;
        }
        AST Replace(TypeOf typeOf) {
            typeOf.Expression.Result = Replace(typeOf.Expression.Result);
            return typeOf;
        }
        AST Replace(New @new) {
            //@new.Expression.Result = Replace(@new.Expression.Result);
            @new.Caller = (Caller)Replace(@new.Caller);
            //@new.Calling = Replace(@new.Calling);
            return @new;
        }
        AST Replace(Binary bin) {
            bin.Left = Replace(bin.Left);
            bin.Right = Replace(bin.Right);
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
                    return new Caller() {
                        Token = bin.Token,
                        Parent = bin.Parent,
                        Function = op,
                        Type = op.Type,
                        Real = op.Real,
                        Parameters = new() { bin.Left, bin.Right },
                    };
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