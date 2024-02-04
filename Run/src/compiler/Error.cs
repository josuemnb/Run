namespace Run {
    public class Error : AST {
        public string Message;
        public string Code;
        public string Path;
        public static readonly string InterfaceOnlyAcceptsPropertiesAndFunctions = "Interface only accepts properties and functions";
        public static readonly string ExpectedPublicAcces = "Expected Public Access";
        public static readonly string ExpectedStaticAcess = "Expected Static Access";
        public static readonly string ExpectingArrowOfBeginOfBlock = "Expecting '=>' or '{'";
        public static readonly string ExpectingOpenParenteses = "Expecting (";
        public static readonly string ExpectingOpenParentesesOrBrackets = "Expecting ( or [";
        public static readonly string ExpectingCommaOrCloseParenteses = "Expecting , or )";
        public static readonly string ExpectingComma = "Expecting ,";
        public static readonly string ExpectingCloseParenteses = "Expecting )";
        public static readonly string ExpectingBeginOfBlock = "Expecting Begin of Block";
        public static readonly string ExpectingBeginOfArray = "Expecting Begin of Array";
        public static readonly string ExpectingEndOfArray = "Expecting End of Array";
        public static readonly string ExpectingEndOfLine = "Expecting End of Line";
        public static readonly string ExpectingName = "Expecting a name";
        public static readonly string ExpectingNumber = "Expecting a number";
        public static readonly string ExpectingMember = "Expecting a member";
        public static readonly string ExpectingValue = "Expecting a Value";
        public static readonly string ExpectingCase = "Expecting a 'case'";
        public static readonly string ExpectingDeclare = "Expecting :";
        public static readonly string ExpectingAssign = "Expecting =";
        public static readonly string ExpetingBlock = "Expecting a Block";
        public static readonly string ExpectingQuote = "Expecting a Quote";

        public static readonly string UnknownFunctionNameOrWrongParamaters = "Unknown function name or wrong paramaters";
        public static readonly string UnknownName = "Unknown name";
        public static readonly string UnknownClassMember = "Unknown class member";
        public static readonly string UnknownType = "Unknown type";
        public static readonly string UndefinedType = "Undefined type";

        public static readonly string IncompatibleType = "Incompatible Types";
        public static readonly string IncompatibleAccessClassStatic = "Invalid Access Definition. Parent Class is Static";

        public static readonly string InvalidAccessDefinition = "Invalid Access Definition. Not Allowed in Modules";
        public static readonly string InvalidAccessFunctionStatic = "Invalid Access Definition. Function is Static";
        public static readonly string InvalidExpression = "Invalid Expression";

        public static readonly string AnnotationsNotAllowedInsideFunctionScope = "Annotation not allowed inside function scope";
        public static readonly string BadFormatted = "Bad Formatted";
        public static readonly string DoubleArrayNotSupported = "Double Array ExpressionV2 Not Supported";
        public static readonly string NameAlreadyExists = "Name already exists";
        public static readonly string TokenAnnotationAlreadyExists = "Token annotation already exists";
        public static readonly string VariadicParameterAlreadyDefined = "No more parameters after variadic parameter definition";
        public static readonly string TypeIsNotArrayDefined = "Type is not @array defined";
        public static readonly string BaseMustBeInsideClass = "Base must be inside of class function member";
        public static readonly string ClassDoesntHasBaseClass = "Class doesn't have a base class definition";
        public static string PathNotFound(string path) => "Path not founded: '" + path + "'";

        public static readonly string OnlyOneParameterAllowedInOperator = "Only One Parameter Allowed in Operator Definition";
        public static readonly string OnlyInClassScope = "Only Allowed inside of Class Scope";
        public static readonly string OnlyInClassOrModuleScope = "Only Allowed inside of Class or Module Scope";
        public static readonly string OnlyInModuleScope = "Only Allowed inside of Module Scope";
        public static readonly string OnlyInFunctionScope = "Only Allowed Inside of Function Scope";
        public static readonly string OnlyInFunctionBlock = "Only Allowed Inside of Block";
        public static readonly string NativeClassNotAllowed = "Member not allowed in native class";
        public static readonly string ScopeOnlyOfConstructorNoParameters = "Scope only accepts constructor with no parameters";
        public static readonly string InterfaceCannotHaveBody = "Interface cannot have body";
        public static readonly string InterfaceOnlyAcceptsPublicMembers = "Interface only accepts public members";
        public static readonly string InterfaceNotAllowPrivateMembers = "Interface not allow private members";
        public static readonly string InterfaceMemberNotFound = "Interface member not found";
        public static readonly string InterfaceCannotBeBased = "Interface cannot have base class or interface";
        public static readonly string GenericNameAlreadyClassDefined = "Generic name already class defined";
        public static readonly string InterfaceMemberHasDifferentReturnType = "Interface member has different return type";
        public static readonly string InterfaceMemberHasDifferentParameters = "Interface member has different parameters";
        public static readonly string InterfaceNotImplementedCorrect = "Interface not implemented correct";
        public static readonly string UnknownTokenType = "Unknown token type";
        public static readonly string ExpectingBaseOrThis = "Expecting base or this";

        public override string ToString() {
            if (Token == null || Token.Value == null) {
                return "Error: " + Message + "\n File: " + Path + "(" + Token?.Line + ":" + Token?.Column + "\n Code: " + Code;
            }
            Code = Code.Replace("\t", "  ");
            var index = Code.IndexOf(Token.Value);
            return "Error: " + Message + "\n File: " + Path + "(" + Token.Line + ":" + Token.Column + ")\nToken: " + Token?.Value + "\n Code: " + Code + "\n" + new string(' ', index + 7) + new string('^', Token.Value.Length);
        }

        internal static void NullType(AST ast) {
            ast.Program.AddError(ast.Scanner.Current, "Null Type: " + ast.Token.Value);
        }
        internal static void InvalidValue(AST ast) {
            ast.Program.AddError(ast.Scanner.Current, "Invalid Value");
        }
    }
}
