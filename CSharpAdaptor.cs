using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CSharp;
using System.IO;
using System.CodeDom.Compiler;

namespace ClassMirror {
    class CSharpAdaptor : GeneratedSource {
        private readonly CSharpCodeProvider _cSharp = new CSharpCodeProvider();
        private static readonly Dictionary<string, Type> _types = new Dictionary<string,Type> {
            { "void", typeof(void) },
            { "int", typeof(int) },
            { "float", typeof(float) },
            { "double", typeof(double) }                                               
        };

        private Member DestructorInterop {
            get {
                return new Member { Name = "Delete" };
            }
        }

        private CodeTypeMember Destructor {
            get {
                return CreateMethodInterop(DestructorInterop);
            }
        }

        public CodeTypeMember Dispose {
            get {
                var disposeImpl = new CodeMemberMethod {
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    Name = "Dispose"
                };
                disposeImpl.Statements.Add(CreateInteropCall(DestructorInterop));
                return disposeImpl;
            }
        }   

        private CodeTypeMember Instance {
            get {
                return new CodeSnippetTypeMember("\t\tinternal readonly IntPtr Instance;");
            }
        }

        private CodeTypeMember[] Constructors {
            get {
                return Ctors.Select(ctor =>  {
                    var result = new CodeConstructor {
                        Attributes = MemberAttributes.Public
                    };
                    result.Statements.Add(new CodeSnippetStatement(string.Format(
                        "\t\t\tInstance = _{0}_{1}({2});",
                        Name,
                        ctor.Name,
                        string.Join(", ", ctor.Params.Select(p => p.Item2))
                    )));
                    return result;
                }).Concat(Ctors.Select(CreateCtorInterop)).ToArray();
            }
        }

        private CodeTypeMember CreateInterop(Member method, bool passInstance) {
            var parms = !method.IsStatic && passInstance ?
                new[] { Tuple.Create("IntPtr", "instance") }.Concat(method.Params).ToArray() :
                method.Params;
            return new CodeSnippetTypeMember(string.Format(
                "\t\t[DllImport(\"{0}\", CallingConvention=CallingConvention.Cdecl)]{1}\t\tprivate static extern {2} _{3}_{4}({5});",
                NativeLib,
                Environment.NewLine,
                method.Type,
                Name,
                method.Name,
                CreateParameterString(parms)
            ));
        }

        private CodeSnippetStatement CreateInteropCall(Member method) {
            var paramNames = method.Params.Select(p => p.Item2);
            if (!method.IsStatic) {
                paramNames = new[] { "Instance" }.Concat(paramNames);
            }
            return new CodeSnippetStatement(string.Format(
                "\t\t\t{0}_{1}_{2}({3});",
                Return(method.Type),
                Name,
                method.Name,
                string.Join(", ", paramNames)));
        }

        private CodeTypeMember CreateCtorInterop(Member method) {
            return CreateInterop(method, false);
        }

        private CodeTypeMember CreateMethodInterop(Member method) {
            return CreateInterop(method, true);
        }

        private CodeParameterDeclarationExpression CreateParameter(Tuple<string, string> param) {
            return new CodeParameterDeclarationExpression(_types[param.Item1], param.Item2);
        }
        
        private CodeTypeMember CreateMethod(Member member) {
            var method = new CodeMemberMethod {
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                Name = member.Name,
                ReturnType =new CodeTypeReference(_types[member.Type])
            };
            if (member.IsStatic) {
                method.Attributes |= MemberAttributes.Static;
            }
            method.Parameters.AddRange(member.Params.Select(CreateParameter).ToArray());
            method.Statements.Add(CreateInteropCall(member));
            return method;
        }

        public string Generate() {
            var compileUnit = new CodeCompileUnit();
            var ns = new CodeNamespace(Namespace);
            ns.Imports.AddRange(new [] {
                new CodeNamespaceImport("System"),
                new CodeNamespaceImport("System.Runtime.InteropServices")
            }); 
            compileUnit.Namespaces.Add(ns);
            var type = new CodeTypeDeclaration(Name);
            type.BaseTypes.Add(new CodeTypeReference("IDisposable"));
            ns.Types.Add(type);
            type.Members.AddRange(
                Constructors.Concat(
                Methods.Select(CreateMethod)).Concat(
                Methods.Select(CreateMethodInterop)).Concat(
                new[] {
                    Dispose,
                    Destructor,
                    Instance
                }).ToArray());
            using (var stringWriter = new StringWriter())
            using (var writer = new IndentedTextWriter(stringWriter, "\t")) {
                _cSharp.GenerateCodeFromCompileUnit(compileUnit, writer, new CodeGeneratorOptions());
                return stringWriter.ToString();
            }
        }
    }
}
