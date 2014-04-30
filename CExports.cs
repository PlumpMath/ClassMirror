using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassMirror {
    class CExports : GeneratedSource {

        private string Include {
            get {
                return string.Format("#include \"{0}\"", Path.GetFileName(Header));
            }
        }

        private string CreateInteropMethod(Member method) {
            return string.Join(Environment.NewLine, new[] {
                string.Format("{0} {1} _{2}_{3}({4}) {{", 
                    Options.Prefix,
                    method.Type,
                    Name,
                    method.Name,
                    CreateParameterString(method.IsStatic ? method.Params : InteropParameter.Concat(method.Params))),
                string.Format("\t{0}{1}{2}({3});",
                    Return(method.Type),
                    method.IsStatic ? string.Format("{0}::", Name) : "instance->",
                    method.Name,
                    string.Join(", ", method.Params.Select(p => p.Item2))),
                "}"
            });
		}

		private static string CreateParameterString(IEnumerable<Tuple<string, string>> parameters) {
			return string.Join(", ", parameters.Select(p => string.Format("{0} {1}", p.Item1, p.Item2)));
		}
        
        private IEnumerable<Tuple<string, string>> InteropParameter {
            get {
				yield return Tuple.Create(Name + "*", "instance");
            }
        }

        private string InteropMethods {
            get {
                return string.Join(DoubleNewline, Methods.Where(IsValid).Select(CreateInteropMethod));
            }
        }

        private bool IsValid(Member method) {
            return HasType(method.Type) && method.Params.Select(p => p.Item1).All(HasType);
        }

        private string UsingNamespace {
            get {
                return string.IsNullOrEmpty(Namespace) ? string.Empty : string.Format("using namespace {0};", Namespace);
            }
        }

        private string InteropConstructors {
            get {
                return string.Join(DoubleNewline, Ctors.Select(ctor =>
                    string.Join(Environment.NewLine, new[] {
                        string.Format("{0} {1}* _{1}_{2}({3}) {{", 
                            Options.Prefix,
                            Name,
                            ctor.Name,
                            CreateParameterString(ctor.Params)),
                        string.Format("\treturn new {0}({1});",
							Name,
							string.Join(", ", ctor.Params.Select(p => p.Item2))),
                        "}"
                    })));
            }
        }

        private string InteropDestructor {
            get {
                return string.Join(Environment.NewLine, new[] {
                    string.Format("{0} void _{1}_Delete({1} *instance) {{", Options.Prefix, Name),
                    "\tdelete instance;",
                    "}"
                });
            }
        }

        public string Generate() {
            return string.Join(DoubleNewline, new[] {
                Include,
                UsingNamespace,
                InteropConstructors,
                InteropDestructor,
                InteropMethods,
                Environment.NewLine
            });
        }

    }
}
