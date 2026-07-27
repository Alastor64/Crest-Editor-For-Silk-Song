using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: inspector <assembly.dll> --list <regex> | --type <Name>");
    return 1;
}

var asm = AssemblyDefinition.ReadAssembly(args[0]);
var mode = args[1];

if (mode == "--list")
{
    var rx = new Regex(args[2]);
    foreach (var t in EnumerateAll(asm.MainModule))
        if (rx.IsMatch(t.FullName))
            Console.WriteLine(t.FullName);
    return 0;
}

if (mode == "--find")
{
    var rx = new Regex(args[2]);
    foreach (var t in EnumerateAll(asm.MainModule))
    {
        foreach (var f in t.Fields)
            if (rx.IsMatch(f.Name)) Console.WriteLine($"{t.FullName} :: F {Sig(f.FieldType)} {f.Name}");
        foreach (var p in t.Properties)
            if (rx.IsMatch(p.Name)) Console.WriteLine($"{t.FullName} :: P {Sig(p.PropertyType)} {p.Name}");
        foreach (var m in t.Methods)
            if (rx.IsMatch(m.Name)) Console.WriteLine($"{t.FullName} :: M {Sig(m.ReturnType)} {m.Name}");
    }
    return 0;
}

if (mode == "--calls")
{
    var rx = new Regex(args[2]);
    foreach (var t in EnumerateAll(asm.MainModule))
    {
        foreach (var m in t.Methods)
        {
            if (!m.HasBody) continue;
            foreach (var instruction in m.Body.Instructions)
            {
                if (instruction.Operand is MethodReference called
                    && rx.IsMatch(called.FullName))
                {
                    Console.WriteLine(
                        $"{t.FullName}::{m.Name} -> {called.FullName}");
                }
                else if (instruction.Operand is FieldReference field
                    && rx.IsMatch(field.FullName))
                {
                    Console.WriteLine(
                        $"{t.FullName}::{m.Name} -> {field.FullName}");
                }
            }
        }
    }
    return 0;
}

if (mode == "--method")
{
    var typeName = args[2];
    var methodRx = new Regex(args[3]);
    var t = EnumerateAll(asm.MainModule)
        .FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
    if (t == null) { Console.Error.WriteLine($"type '{typeName}' not found"); return 2; }

    foreach (var m in t.Methods.Where(x => methodRx.IsMatch(x.Name)))
    {
        Console.WriteLine(
            $"// {t.FullName}::{m.Name}({string.Join(", ", m.Parameters.Select(p => Sig(p.ParameterType)))})");
        if (!m.HasBody)
        {
            Console.WriteLine("(no body)");
            continue;
        }
        foreach (var instruction in m.Body.Instructions)
            Console.WriteLine(instruction);
    }
    return 0;
}

if (mode == "--type")
{
    var name = args[2];
    var t = EnumerateAll(asm.MainModule).FirstOrDefault(x => x.Name == name || x.FullName == name);
    if (t == null) { Console.Error.WriteLine($"type '{name}' not found"); return 2; }

    Console.WriteLine($"// {t.FullName}");
    foreach (var f in t.Fields.OrderBy(f => f.Name))
        Console.WriteLine($"F  [{Flags(f)}] {Sig(f.FieldType),-22} {f.Name}");
    foreach (var p in t.Properties.OrderBy(p => p.Name))
        Console.WriteLine($"P  [{(p.GetMethod?.IsStatic == true ? 'S' : 'I')}] {Sig(p.PropertyType),-22} {p.Name}");
    foreach (var m in t.Methods.OrderBy(m => m.Name))
        Console.WriteLine($"M  [{(m.IsStatic ? 'S' : 'I')}] {Sig(m.ReturnType),-22} {m.Name}({string.Join(", ", m.Parameters.Select(p => Sig(p.ParameterType)))})");
    return 0;
}

Console.Error.WriteLine("unknown mode");
return 1;

static string Sig(TypeReference? tr) => tr?.Name ?? "?";

static string Flags(FieldDefinition f)
{
    string s = f.IsStatic ? "S" : "I";
    if (f.IsLiteral) s += "L";
    else if (f.IsInitOnly) s += "R";
    return s;
}

static IEnumerable<TypeDefinition> EnumerateAll(ModuleDefinition module)
{
    foreach (var t in module.Types)
    {
        yield return t;
        foreach (var n in EnumerateNested(t))
            yield return n;
    }
}

static IEnumerable<TypeDefinition> EnumerateNested(TypeDefinition t)
{
    foreach (var n in t.NestedTypes)
    {
        yield return n;
        foreach (var nn in EnumerateNested(n))
            yield return nn;
    }
}
