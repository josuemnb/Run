
using System;
using System.IO;

namespace Run {
    public static class Keywords {

        internal static void ParseThis(Block parent) {
            parent.Scanner.RollBack();
            if (parent is Class) {
                var ctor = parent.Add<Constructor>();
                ctor.Type = parent as Class;
                ctor.Parse();
                return;
            }
            parent.Add<ExpressionV2>().Parse();
        }

        internal static void ParseDefer(Block parent) {
            var func = parent.FindParent<Function>();
            if (func == null) {
                parent.Program.AddError(parent.Scanner.Current, Error.OnlyInFunctionBlock);
                parent.Scanner.SkipLine();
                return;
            }
            func.HasDefers = true;
            if (parent.Defers.Count == 0) {
                Block.DeferCounter++;
            }
            var defer = parent.Add<Defer>();
            defer.ID = Block.DeferCounter;
            parent.Defers.Add(defer);
            defer.Parse();
        }

        internal static void ParseVar(Block parent) {
            if (parent is not Block) {
                parent.Program.AddError(parent.Scanner.Current, Error.OnlyInFunctionBlock);
                parent.Scanner.SkipLine();
                return;
            }
        again:
            switch (parent) {
                case Module:
                    parent.Add<Global>().Parse();
                    break;
                case Class cls:
                    if (cls.IsNative) {
                        //parent.Program.AddError(parent.Scanner.Current, Error.NativeClassNotAllowed);
                        //parent.Scanner.SkipLine();
                        //return;
                    }
                    parent.Add<Field>().Parse();
                    break;
                default:
                    parent.Add<Var>().Parse();
                    break;
            }
            //if (parent is Class cls && cls.IsNative) {
            //    parent.Program.AddError(parent.Scanner.Current, Error.NativeClassNotAllowed);
            //    parent.Scanner.SkipLine();
            //    return;
            //}
            //parent.Add<Var>().Parse();
            if (parent.Scanner.Expect(',')) {
                goto again;
            }
        }

        internal static void CheckAndParse<T>(Block parent, Func<bool> predicate) where T : AST, new() {
            if (predicate()) {
                parent.Add<T>().Parse();
            } else {
                parent.Program.AddError(parent.Scanner.Current, "Expecting " + typeof(T).Name);
            }
        }

        internal static bool Parse(Token token, Block parent) {
            switch (token.Value) {
                case "internal":
                case "protected":
                case "public":
                case "private": CheckModifier(token, parent); break;
                case "if": CheckAndParse<If>(parent, () => parent.FindParent<Function>() != null); break;
                case "for": CheckAndParse<For>(parent, () => parent.FindParent<Function>() != null); break;
                case "var": ParseVar(parent); break;
                case "enum": ParseEnum(parent); break;
                case "func": ParseFunction(parent); break;
                case "goto": CheckAndParse<Goto>(parent, () => parent.FindParent<Function>() != null); break;
                case "main": CheckAndParse<Main>(parent, () => parent is Module); break;
                case "this": ParseThis(parent); break;
                case "type": CheckAndParse<Class>(parent, () => parent is Module); break;
                case "break": CheckAndParse<Break>(parent, () => parent.FindParent<For>() != null); break;
                case "defer": ParseDefer(parent); break;
                case "label": CheckAndParse<Label>(parent, () => parent.FindParent<Function>() != null); break;
                case "using": CheckAndParse<Using>(parent, () => parent is Module); break;
                case "return": CheckAndParse<Return>(parent, () => parent.FindParent<Function>() is Function); break;
                case "static": AST.CurrentAccess = AccessType.STATIC; break;
                case "switch": CheckAndParse<Switch>(parent, () => parent.FindParent<Function>() != null); break;
                case "library": CheckAndParse<Library>(parent, () => parent is Module); break;
                case "continue": CheckAndParse<Continue>(parent, () => parent.FindParent<For>() != null); break;
                case "operator": CheckAndParse<Operator>(parent, () => parent is Class cls && cls.IsNumber == false); break;
                case "property": CheckAndParse<Property>(parent, () => parent is Class); break;
                case "interface": CheckAndParse<Interface>(parent, () => parent is Module); break;
                case "namespace": CheckAndParse<Namespace>(parent, () => parent.Scanner.Line == 1); break;
                default: return false;
            }
            return true;
        }

        internal static void ParseEnum(Block parent) {
            if (parent is not Module) {
                parent.Program.AddError(parent.Scanner.Current, Error.OnlyInModuleScope);
                return;
            }
            parent.Add<Enum>().Parse();
        }

        internal static void ParseFunction(Block parent) {
            if (parent is Function || parent.FindParent<Function>() != null) {
                parent.Program.AddError(parent.Scanner.Current, Error.OnlyInClassOrModuleScope);
                return;
            }
            parent.Add<Function>().Parse();
        }

        internal static void CheckModifier(Token token, Block parent) {
            if (parent is not Class && parent is not Module) {
                parent.Program.AddError(token, Error.OnlyInClassOrModuleScope);
                return;
            }
            switch (token.Value) {
                case "public": AST.CurrentModifier = AccessModifier.PUBLIC; break;
                case "internal": AST.CurrentModifier = AccessModifier.INTERNAL; break;
                case "protected": AST.CurrentModifier = AccessModifier.PROTECTED; break;
                case "private": AST.CurrentModifier = AccessModifier.PRIVATE; break;
            }
        }
    }
}
