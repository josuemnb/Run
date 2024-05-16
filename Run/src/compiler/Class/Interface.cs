namespace Run {
    public class Interface : Class {

        public override void Parse() {
            base.Parse();
            if (IsBased) {
                Program.AddError(Base.Token, Error.InterfaceCannotBeBased);
            }
            if (HasInterfaces) {
                Program.AddError(Interfaces[0].Token, Error.InterfaceCannotBeBased);
            }
            for (int i = Children.Count - 1; i >= 0; i--) {
                var child = Children[i];
                if (child.Access != AccessType.INSTANCE) {
                    Program.AddError(child.Token, Error.InterfaceOnlyAcceptsPublicMembers);
                }
                if (child.Modifier == AccessModifier.PRIVATE) {
                    Program.AddError(child.Token, Error.InterfaceNotAllowPrivateMembers);
                }
                switch (child) {
                    case Function func:
                        Validate(func);
                        break;
                    case Property prop:
                        Validate(prop.Getter);
                        Validate(prop.Setter);
                        break;
                    default:
                        Program.AddError(child.Token, Error.InterfaceOnlyAcceptsPropertiesAndFunctions);
                        break;
                }
            }
        }

        void Validate(Block block) {
            if (block == null || block.Children.Count == 0) return;
            if (block.Children.Count == 1 && block.Children[0] is Block) {
            } else {
                Program.AddError(block.Token, Error.InterfaceCannotHaveBody);
            }
        }
    }
}
