using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassMirror {
    class GeneratedSource {
        public string NativeLib;
        public string Name;
        public string Namespace;
        public string Header;
        public Options Options;
        public IList<Member> Methods;
        public IList<Member> Ctors;
        protected static readonly Dictionary<string, Type> Types = new Dictionary<string, Type> {
            { "void", typeof(void) },
            { "int", typeof(int) },
            { "float", typeof(float) },
            { "double", typeof(double) },
            { "char*", typeof(string) }
        };

        public bool IsParsedType(string type) {
            return Options.Sources.Any(s => s.Name == type.TrimEnd('*'));
        }

        public static Type GetManagedType(string nativeType) {
            if (string.IsNullOrEmpty(nativeType)) {
                throw new Exception("Type is unknown");
            }
            Type result;
            if (Types.TryGetValue(nativeType, out result)) {
                return result;
            }
            if (nativeType.EndsWith("*")) {
                return typeof(IntPtr);
            }
            throw new Exception("Could not map type " + nativeType);
        }

        public static string GetInteropReturnType(string nativeType) {
            return nativeType.EndsWith("*") ? "IntPtr" : nativeType;
        }

        public static bool HasType(string nativeType) {
            return nativeType.EndsWith("*") || Types.ContainsKey(nativeType);
        }

        protected static string Return(string type) {
            return type == "void" ? string.Empty : "return ";
        }

        protected static string DoubleNewline {
            get {
                return string.Format("{0}{0}", Environment.NewLine);
            }
        }
    }
}
