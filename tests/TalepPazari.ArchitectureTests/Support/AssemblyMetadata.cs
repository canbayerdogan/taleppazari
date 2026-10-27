using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace TalepPazari.ArchitectureTests.Support;

// Reads a compiled assembly with System.Reflection.Metadata (part of the BCL) for the checks ArchUnitNET
// does not cover: every custom attribute (including those on parameters) and the IL of a method body.
internal sealed class AssemblyMetadata : IDisposable
{
    private static readonly Dictionary<ushort, OperandType> OperandTypes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => (ushort)o.Value, o => o.OperandType);

    private readonly PEReader peReader;
    private readonly MetadataReader reader;

    private AssemblyMetadata(string path)
    {
        peReader = new PEReader(File.OpenRead(path));
        reader = peReader.GetMetadataReader();
    }

    public static AssemblyMetadata Open(string path) => new(path);

    public void Dispose() => peReader.Dispose();

    // Every attribute in the assembly: (attribute type full name, what it is applied to).
    public IEnumerable<(string AttributeType, string AppliedTo)> CustomAttributes()
    {
        var parameterOwners = reader.MethodDefinitions
            .SelectMany(m => reader.GetMethodDefinition(m).GetParameters().Select(p => (Parameter: p, Method: m)))
            .ToDictionary(x => x.Parameter, x => x.Method);

        foreach (var handle in reader.CustomAttributes)
        {
            var attribute = reader.GetCustomAttribute(handle);
            yield return (AttributeTypeName(attribute.Constructor), Describe(attribute.Parent, parameterOwners));
        }
    }

    // Every type the assembly references from another assembly: (assembly name, type full name).
    // The TypeRef table covers all compiled code, including lambdas and top-level statements.
    public IEnumerable<(string Assembly, string Type)> ReferencedTypes()
    {
        foreach (var handle in reader.TypeReferences)
        {
            var outermost = reader.GetTypeReference(handle);
            while (outermost.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                outermost = reader.GetTypeReference((TypeReferenceHandle)outermost.ResolutionScope);
            }

            if (outermost.ResolutionScope.Kind == HandleKind.AssemblyReference)
            {
                var assembly = reader.GetAssemblyReference((AssemblyReferenceHandle)outermost.ResolutionScope);
                yield return (reader.GetString(assembly.Name), TypeName(handle));
            }
        }
    }

    // The value of a string constant field, or null when the type or the constant does not exist.
    public string? StringConstant(string typeFullName, string fieldName)
    {
        var type = FindType(typeFullName);
        if (type is null)
        {
            return null;
        }

        foreach (var handle in reader.GetTypeDefinition(type.Value).GetFields())
        {
            var field = reader.GetFieldDefinition(handle);
            var constant = field.GetDefaultValue();
            if (reader.GetString(field.Name) == fieldName && !constant.IsNil)
            {
                var blob = reader.GetBlobReader(reader.GetConstant(constant).Value);
                return blob.ReadUTF16(blob.Length);
            }
        }

        return null;
    }

    // String literals passed directly to a call of calleeName inside typeFullName.methodName:
    // the IL pattern `ldstr "value"` immediately followed by `call`/`callvirt calleeName`.
    // A const string argument compiles to exactly this pattern; a value from a variable or a call does not.
    public IReadOnlyList<string> StringLiteralsPassedTo(string typeFullName, string methodName, string calleeName)
    {
        var found = new List<string>();
        var type = FindType(typeFullName);
        if (type is null)
        {
            return found;
        }

        foreach (var handle in reader.GetTypeDefinition(type.Value).GetMethods())
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName || method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var il = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
            string? lastLiteral = null;
            while (il.RemainingBytes > 0)
            {
                int code = il.ReadByte();
                if (code == 0xFE)
                {
                    code = 0xFE00 | il.ReadByte();
                }

                var operandType = OperandTypes[(ushort)code];
                if (operandType == OperandType.InlineString)
                {
                    lastLiteral = reader.GetUserString(MetadataTokens.UserStringHandle(il.ReadInt32() & 0x00FFFFFF));
                    continue;
                }

                if (operandType == OperandType.InlineMethod)
                {
                    var callee = MetadataTokens.EntityHandle(il.ReadInt32());
                    if (lastLiteral is not null && MethodName(callee) == calleeName)
                    {
                        found.Add(lastLiteral);
                    }
                }
                else
                {
                    SkipOperand(ref il, operandType);
                }

                lastLiteral = null;
            }
        }

        return found;
    }

    private static void SkipOperand(ref BlobReader il, OperandType operandType)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                break;
            case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                il.Offset += 1;
                break;
            case OperandType.InlineVar:
                il.Offset += 2;
                break;
            case OperandType.InlineI8 or OperandType.InlineR:
                il.Offset += 8;
                break;
            case OperandType.InlineSwitch:
                il.Offset += 4 * il.ReadInt32();
                break;
            default:
                il.Offset += 4;
                break;
        }
    }

    private TypeDefinitionHandle? FindType(string fullName) =>
        reader.TypeDefinitions.Cast<TypeDefinitionHandle?>().FirstOrDefault(h => TypeName(h!.Value) == fullName);

    private string MethodName(EntityHandle handle) => handle.Kind switch
    {
        HandleKind.MethodDefinition => reader.GetString(reader.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
        HandleKind.MemberReference => reader.GetString(reader.GetMemberReference((MemberReferenceHandle)handle).Name),
        HandleKind.MethodSpecification => MethodName(reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method),
        _ => string.Empty,
    };

    private string AttributeTypeName(EntityHandle constructor) => constructor.Kind switch
    {
        HandleKind.MethodDefinition => TypeName(reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType()),
        HandleKind.MemberReference => TypeName(reader.GetMemberReference((MemberReferenceHandle)constructor).Parent),
        _ => string.Empty,
    };

    private string TypeName(EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => TypeName((TypeDefinitionHandle)handle),
        HandleKind.TypeReference => TypeName((TypeReferenceHandle)handle),
        _ => string.Empty,
    };

    // Full name with '+' for nested types, as ArchUnitNET writes it.
    private string TypeName(TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var declaring = type.GetDeclaringType();
        return declaring.IsNil
            ? Join(reader.GetString(type.Namespace), reader.GetString(type.Name))
            : TypeName(declaring) + "+" + reader.GetString(type.Name);
    }

    private string TypeName(TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        return type.ResolutionScope.Kind == HandleKind.TypeReference
            ? TypeName((TypeReferenceHandle)type.ResolutionScope) + "+" + reader.GetString(type.Name)
            : Join(reader.GetString(type.Namespace), reader.GetString(type.Name));
    }

    private string Describe(EntityHandle parent, Dictionary<ParameterHandle, MethodDefinitionHandle> parameterOwners) => parent.Kind switch
    {
        HandleKind.TypeDefinition => TypeName((TypeDefinitionHandle)parent),
        HandleKind.MethodDefinition => DescribeMethod((MethodDefinitionHandle)parent),
        HandleKind.FieldDefinition => DescribeField((FieldDefinitionHandle)parent),
        HandleKind.PropertyDefinition => DescribeProperty((PropertyDefinitionHandle)parent),
        HandleKind.Parameter => DescribeParameter((ParameterHandle)parent, parameterOwners),
        HandleKind.AssemblyDefinition => "assembly",
        HandleKind.ModuleDefinition => "module",
        _ => parent.Kind.ToString(),
    };

    private string DescribeMethod(MethodDefinitionHandle handle)
    {
        var method = reader.GetMethodDefinition(handle);
        return TypeName(method.GetDeclaringType()) + "." + reader.GetString(method.Name);
    }

    private string DescribeField(FieldDefinitionHandle handle)
    {
        var field = reader.GetFieldDefinition(handle);
        return TypeName(field.GetDeclaringType()) + "." + reader.GetString(field.Name);
    }

    private string DescribeProperty(PropertyDefinitionHandle handle)
    {
        var property = reader.GetPropertyDefinition(handle);
        var accessors = property.GetAccessors();
        var accessor = accessors.Getter.IsNil ? accessors.Setter : accessors.Getter;
        var owner = accessor.IsNil ? "?" : TypeName(reader.GetMethodDefinition(accessor).GetDeclaringType());
        return owner + "." + reader.GetString(property.Name);
    }

    private string DescribeParameter(ParameterHandle handle, Dictionary<ParameterHandle, MethodDefinitionHandle> parameterOwners)
    {
        var name = reader.GetString(reader.GetParameter(handle).Name);
        return parameterOwners.TryGetValue(handle, out var method)
            ? $"{DescribeMethod(method)}(parameter {name})"
            : $"parameter {name}";
    }

    private static string Join(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
}
