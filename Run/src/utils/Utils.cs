using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Run {
    public static class Utils {

        static MethodInfo clone;

        public static T Clone<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(this T obj) where T : class {
            if (clone == null) {
                clone = typeof(T).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return (T)clone.Invoke(obj, null);
        }
    }
}
