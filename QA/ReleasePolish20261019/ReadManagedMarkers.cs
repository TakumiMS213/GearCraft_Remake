// QA-only static metadata/IL reader. Never loads the target into the runtime.
using System;
using System.Globalization;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class ReadManagedMarkers {
    static string B(string value) { return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? "")); }
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types) {
        foreach (var type in types) { yield return type; foreach(var nested in Types(type.NestedTypes)) yield return nested; }
    }
    public static int Main(string[] args) {
        if (args.Length != 1) { Console.Error.WriteLine("Expected one managed assembly path"); return 2; }
        try {
            using(var assembly=AssemblyDefinition.ReadAssembly(args[0],new ReaderParameters { ReadSymbols=false, ReadingMode=ReadingMode.Deferred })) {
                Console.WriteLine("ASSEMBLY\t"+B(assembly.Name.FullName)+"\t"+B(assembly.MainModule.Mvid.ToString()));
                foreach(var type in Types(assembly.MainModule.Types)) {
                    string owner=type.FullName;
                    bool ending=owner=="EndingManager"||owner.StartsWith("EndingManager/");
                    bool audio=owner=="Audio";
                    bool cadence=owner=="PlayerAttackCadence";
                    bool player=owner=="PlayerController";
                    if(!ending&&!audio&&!cadence&&!player)continue;
                    Console.WriteLine("TYPE\t"+B(owner));
                    foreach(var field in type.Fields) {
                        if(!field.HasConstant)continue;
                        Console.WriteLine("CONSTANT\t"+B(owner)+"\t"+B(field.Name)+"\t"+B(Convert.ToString(field.Constant,CultureInfo.InvariantCulture)));
                    }
                    foreach(var method in type.Methods) {
                        if(player&&method.Name!="ProcessAttackInput"&&method.Name!="Update"&&method.Name!=".ctor")continue;
                        Console.WriteLine("METHOD\t"+B(owner)+"\t"+B(method.FullName));
                        if(!method.HasBody)continue;
                        foreach(var instruction in method.Body.Instructions) {
                            var reference=instruction.Operand as MethodReference;
                            if(reference!=null)Console.WriteLine("CALL\t"+B(owner)+"\t"+B(method.FullName)+"\t"+B(reference.FullName)+"\t"+instruction.Offset);
                            if(instruction.OpCode.Code==Code.Ldstr)Console.WriteLine("STRING\t"+B(owner)+"\t"+B(method.FullName)+"\t"+B((string)instruction.Operand));
                        }
                    }
                }
            }
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
