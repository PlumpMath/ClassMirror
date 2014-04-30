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
                return Ctors.Select(ctor => {
                    try {
                        var result = new CodeConstructor {
                            Attributes = MemberAttributes.Public
                        };
                        result.Parameters.AddRange(ctor.Params.Select(CreateParameter).ToArray());
                        result.Statements.Add(new CodeSnippetStatement(string.Format(
                            "\t\t\tInstance = _{0}_{1}({2});",
                            Name,
                            ctor.Name,
                            string.Join(", ", ctor.Params.Select(p => p.Item2))
                        )));
                        return result;
                    } catch (Exception e) {
                        Console.WriteLine(e.Message);
                        return null;
                    }
                }).Where(ctor => ctor != null)
                    .Concat(Ctors.Select(CreateCtorInterop))
                    .Concat(new [] { IntPtrConstructor }).ToArray();
            }
        }

        private CodeTypeMember IntPtrConstructor {
            get {
                var result = new CodeConstructor {
                    Attributes = MemberAttributes.Assembly
                };
                result.Parameters.Add(new CodeParameterDeclarationExpression("IntPtr", "instance"));
                result.Statements.Add(new CodeSnippetStatement("\t\t\tInstance = instance;"));
                return result;
            }
        }

        private CodeTypeMember CreateInterop(Member method, bool passInstance) {
            try {
                var parms = !method.IsStatic && passInstance ?
				new[] { Tuple.Create(Name + "*", "instance") }.Concat(method.Params).ToArray() :
                method.Params;
                return new CodeSnippetTypeMember(string.Format(
                    "\t\t[DllImport(\"{0}\", CallingConvention=CallingConvention.Cdecl)]{1}\t\tprivate static extern {2} _{3}_{4}({5});",
                    NativeLib,
                    Environment.NewLine,
                    GetInteropReturnType(method.Type),
                    Name,
                    method.Name,
                    CreateParameterString(parms)
                ));          
            } catch (Exception e) {
                Console.WriteLine("Could not parse {0}: {1}, continuing", method.Name, e.Message);
                return null;
            }
        }

        private string GetArgumentName(Tuple<string, string> parameter) {
            string paramName = parameter.Item2 == string.Empty ? "unused" : parameter.Item2;
            if (IsParsedType(parameter.Item1)) {
                paramName += ".Instance";
            }
            return paramName;
        }

        private string GetParamName(string paramName) {
            return paramName == string.Empty ? "unused" : paramName;
        }

        private CodeSnippetStatement CreateInteropCall(Member method, int indentation = 3) {
            var paramNames = method.Params.Select(p => GetArgumentName(p));
            if (!method.IsStatic) {
                paramNames = new[] { "Instance" }.Concat(paramNames);
            }
            return IsParsedType(method.Type) ?
                new CodeSnippetStatement(string.Format(
                    "{0}return new {1}(_{2}_{3}({4}));",
                    new string('\t', indentation),
                    method.Type.TrimEnd('*'),
                    Name,
                    method.Name,
                    string.Join(", ", paramNames))) :
                new CodeSnippetStatement(string.Format(
                    "{0}{1}_{2}_{3}({4});",
                    new string('\t', indentation),
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
            return new CodeParameterDeclarationExpression(GetPublicManagedType(param.Item1), GetParamName(param.Item2));
        }

        public static CodeTypeReference GetPublicReturnType(string nativeType) {
            if (nativeType.EndsWith("*")) {
                return new CodeTypeReference(nativeType.Substring(0, nativeType.Length - 1));
            }
            Type result;
            if (Types.TryGetValue(nativeType, out result)) {
                return new CodeTypeReference(result);
            }
            throw new Exception("Could not parse " + nativeType);
        }

        private string CreateParameterString(IEnumerable<Tuple<string, string>> parameters) {
            return string.Join(", ", parameters.Select(p => string.Format("{0} {1}", GetManagedType(p.Item1), GetParamName(p.Item2))));
        }

        private CodeTypeMember CreateMethod(Member member) {
            try {
                var method = new CodeMemberMethod {
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    Name = member.Name,
                    ReturnType = GetPublicReturnType(member.Type)
                };
                if (member.IsStatic) {
                    method.Attributes |= MemberAttributes.Static;
                }
                method.Parameters.AddRange(member.Params.Select(CreateParameter).ToArray());
                method.Statements.Add(CreateInteropCall(member));
                return method;
            } catch (Exception e) {
                Console.WriteLine("Could not parse {0}: {1}, continuing", member.Name, e.Message);
                return null;
            }
        }

        private CodeTypeMember CreateProperty(IGrouping<string, Member> members) {
            try {
                if (members.Any(m => m.IsStatic != members.First().IsStatic)) {
                    throw new Exception(string.Format("Mismatched staticness for {0} accessors", members.Key));
                }
                var getter = members.SingleOrDefault(m => m.IsGetter);
                var setter = members.SingleOrDefault(m => m.IsSetter);
                if (getter != null && getter.Params.Count > 0) {
                    throw new Exception(string.Format("{0} has parameter", getter.Name));
                }
                if (setter != null && setter.Params.Count != 1) {
                    throw new Exception(string.Format("{0} does not have one parameter", setter.Name));
                }
                string type = getter == null ? setter.Params.First().Item1 : getter.Type;
                var property = new CodeMemberProperty {
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    Name = members.Key,
                    Type = GetPublicReturnType(type),
                    HasGet = getter != null,
                    HasSet = setter != null
                };
                if (members.First().IsStatic) {
                    property.Attributes |= MemberAttributes.Static;
                }
                if (setter != null) {
                    property.SetStatements.Add(CreateInteropCall(new Member {
                        Name = setter.Name,
                        Params = setter.Params.Select(p => Tuple.Create(p.Item1, "value")).ToList(),
                        Type = setter.Type,
                        IsStatic = setter.IsStatic
                    }, 4));
                }
                if (getter != null) {
                    property.GetStatements.Add(CreateInteropCall(getter, 4));
                }
                return property;
            } catch (Exception e) {
                Console.WriteLine("Could not parse {0}: {1}, continuing", members.Key, e.Message);
                return null;
            }
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
            var methods = Methods.Where(m => !m.IsProperty);
            var properties = Methods.Where(m => m.IsProperty).GroupBy(m => m.Name.Substring(3));
            type.Members.AddRange(
                Constructors.Concat(
                    methods.Select(CreateMethod)).Concat(
                    properties.Select(CreateProperty)).Concat(
                    Methods.Select(CreateMethodInterop)).Concat(
                    new[] {
                        Dispose,
                        Destructor,
                        Instance
                    }).Where(m => m != null).ToArray());
            using (var stringWriter = new StringWriter())
            using (var writer = new IndentedTextWriter(stringWriter, "\t")) {
                _cSharp.GenerateCodeFromCompileUnit(compileUnit, writer, new CodeGeneratorOptions());
                return stringWriter.ToString();
            }
        }
    }
}
