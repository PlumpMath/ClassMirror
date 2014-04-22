using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassMirror {
    class Member {
        public string Name;
        public IList<Tuple<string, string>> Params = new List<Tuple<string, string>>();
        public string Type = "void";
        public bool IsStatic = false;
    }
}
