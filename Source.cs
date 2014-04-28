using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassMirror {
    class Source {

        public readonly string[] Namespaces;
        public readonly string Name, Header, CExports, CsGen;

        public Source(string type, string header, string cExports, string csGen) {
            var typeParts = type.Split(new[] { "::" }, StringSplitOptions.None);
            Name = typeParts.Last();
            Namespaces = typeParts.Take(typeParts.Length - 1).ToArray();
            Header = header;
            CExports = cExports;
            CsGen = csGen;
        }
    }
}
