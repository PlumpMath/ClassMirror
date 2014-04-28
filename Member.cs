using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ClassMirror {
    class Member {

        public string Name;
        public IList<Tuple<string, string>> Params = new List<Tuple<string, string>>();
        public string Type = "void";
        public bool IsStatic = false;
        public bool IsGetter {
            get {
                return Regex.IsMatch(Name, "^[gG]et[A-Z].*");
            }
        }
        public bool IsSetter {
            get {
                return Regex.IsMatch(Name, "^[sS]et[A-Z].*");
            }
        }
        public bool IsProperty {
            get {
                return IsSetter || IsGetter;
            }
        }
    }
}
