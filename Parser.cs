    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClangSharp;
using System.IO;
using File = System.IO.File;

namespace ClassMirror {
    class Parser {

        static bool Debug;

        static int Main(string[] args) {
            string config = args.FirstOrDefault(File.Exists);
            Debug = args.Any(a => a == "--debug");
            if (config == null) {
                Console.WriteLine("Config file not found");
                return 2;
            }
            if (args.Any(arg => arg == "-w")) {
                Options.ConfigurationError += Console.WriteLine;
                Options.ConfigurationChanged += o => Run(o);
                Options.StartWatching(config);
                Console.WriteLine("Watching, press q to quit.");
                while (true) {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q') {
                        return 0;
                    }
                }
            }
            return Run(Options.Load(config));
        }

        static int Run(Options options) {
            int errorCount = 0;
            var includes = options.Includes.Select(i => "-I" + i).ToArray();
            foreach (var source in options.Sources) {
                string headerPath = Path.Combine(options.BaseDir, source.Header);
                if (File.GetLastWriteTime(headerPath) < File.GetLastWriteTime(source.CExports) &&
                    File.GetLastWriteTime(options.ConfigFile) < File.GetLastWriteTime(source.CExports)) {
                    Console.WriteLine("Files generated from {0} are up to date", source.Header);
                    if (!Debug) {
                        continue;
                    }
                }
                string tempCppFile = Path.GetTempPath() + Guid.NewGuid().ToString() + ".cpp";
                try {
                    var cSharp = new CSharpAdaptor {
                        Name = source.Name,
                        NativeLib = options.DllName,
                        Options = options
                    };
                    var cExports = new CExports {
                        Name = source.Name,
                        Header = source.Header,
                        Options = options
                    };
                    File.WriteAllText(tempCppFile, string.Format("#include \"{0}\"", headerPath));
                    using (var index = new Index(true, Debug))
                    using (var tu = index.CreateTranslationUnit(tempCppFile, includes)) {
                        string errors = string.Join(Environment.NewLine,
                            from diagnostic in tu.Diagnostics
                            where diagnostic.Level == Diagnostic.Severity.Error || diagnostic.Level == Diagnostic.Severity.Fatal
                            select diagnostic.Description);
                        if (errors != string.Empty && Debug) {
                            Console.WriteLine("Continuing with errors in {0}: {1}", source.Header, errors);
                        }
                        var typeDecl = FindTypeDecl(tu.Cursor, source.Name);
                        var namespaces = GetNamespaces(typeDecl);
                        var methods = typeDecl.Children.Where(child =>
                            child.Kind == CursorKind.CxxMethod &&
                            child.AccessSpecifier == AccessSpecifier.Public &&
                            GeneratedSource.HasType(child.ResultType.Spelling)
                        ).ToArray();
                        var ctors = FindCtors(typeDecl).ToArray();
                        cSharp.Namespace = string.Join(".", namespaces);
                        cExports.Namespace = string.Join("::", namespaces);
                        cExports.Ctors = cSharp.Ctors = ctors.Length == 0 ?
                            new[] { new Member {
                                Name = "New",
                                Type = "IntPtr"
                            } } :
                            ctors;
                        cExports.Methods = cSharp.Methods = methods.Select(m => new Member {
                            Name = m.Spelling,
                            Type = m.Type.Result.Spelling,
                            Params = CreateParams(m),
                            IsStatic = m.IsStaticCxxMethod
                        }).Where(options.CanInterop).ToList();
                        File.WriteAllText(source.CsGen, cSharp.Generate());
                        File.WriteAllText(source.CExports, cExports.Generate());
                    }
                } catch (Exception exception) {
                    Console.WriteLine(exception.Message);
                    errorCount++;
                } finally {
                    File.Delete(tempCppFile);
                }
            }
            return errorCount;
        }

        static IEnumerable<Member> FindCtors(Cursor type) {
            int i = 0;
            return from child in type.Children
                   where child.Kind == CursorKind.Constructor
                   select new Member {
                       Params = CreateParams(child),
                       Name = "New" + i++,
                       Type = "IntPtr"
                   };
        }

        static List<Tuple<string, string>> CreateParams(Cursor method) {
            var result = new List<Tuple<string, string>>();
            method.VisitChildren((c, p) => {
                if (c.Kind == CursorKind.ParmDecl) {
                    result.Add(Tuple.Create(c.Type.Spelling, c.Spelling));
                }
                return Cursor.ChildVisitResult.Recurse;
            });
            return result;
        }

        static Cursor FindTypeDecl(Cursor cursor, string name) {
            return cursor.Descendants.First(c =>
                (c.Kind == CursorKind.ClassDecl || c.Kind == CursorKind.StructDecl) && c.Spelling == name);
        }

        static IList<string> GetNamespaces(Cursor cursor) {
            var result = new List<string>();
            for (; !cursor.IsNull; cursor = cursor.SemanticParent) {
                if (cursor.Kind == CursorKind.Namespace) {
                    result.Add(cursor.Spelling);
                }
            }
            result.Reverse();
            return result;
        }
    }
}
