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

        protected static string CreateParameterString(IEnumerable<Tuple<string, string>> parameters) {
            return string.Join(", ", parameters.Select(p => string.Format("{0} {1}", p.Item1, p.Item2)));
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
