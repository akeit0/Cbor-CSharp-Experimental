using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cbor.SourceGenerator;

/// <summary>Generates explicit, closed CBOR formatter graphs without reflection.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class CborGenerator : IIncrementalGenerator
{
    private const int MaxGraphTypes = 4096;
    private const int MaxTypeNesting = 64;
    private const int MaxTypeComponents = 4096;
    private static readonly DiagnosticDescriptor InvalidContract = new(
        "CBOR001", "Invalid CBOR contract", "{0}", "Cbor", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor UnsupportedType = new(
        "CBOR002", "Unsupported CBOR type", "{0}", "Cbor", DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var resolvers = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Cbor.CborResolverAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (attribute, _) => attribute);
        context.RegisterSourceOutput(resolvers, static (output, attribute) => Generate(output, attribute));
    }

    private static void Generate(SourceProductionContext output, GeneratorAttributeSyntaxContext attribute)
    {
        var resolver = (INamedTypeSymbol)attribute.TargetSymbol;
        var declaration = (ClassDeclarationSyntax)attribute.TargetNode;
        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword) || resolver.ContainingType is not null ||
            resolver.Arity != 0 || resolver.IsStatic || resolver.IsAbstract ||
            declaration.Modifiers.Any(static modifier => modifier.ValueText == "file") ||
            !resolver.InstanceConstructors.Any(static constructor => constructor.Parameters.Length == 0) ||
            (resolver.BaseType is not null && resolver.BaseType.SpecialType != SpecialType.System_Object) ||
            resolver.GetMembers("Instance").Length != 0 || resolver.GetMembers("GetFormatter").Length != 0 ||
            resolver.GetMembers("Provider").Length != 0 || resolver.GetMembers("GeneratedFactory").Length != 0)
        {
            Report(output, resolver, "A CBOR resolver must be a top-level, non-file-local, nongeneric, nonabstract partial class with a parameterless constructor, without a base class or reserved Instance, GetFormatter, Provider, or GeneratedFactory members.");
            return;
        }

        var compilation = attribute.SemanticModel.Compilation;
        var arguments = attribute.Attributes[0].ConstructorArguments;
        if (arguments.Length != 1 || arguments[0].Kind != TypedConstantKind.Array || arguments[0].IsNull || arguments[0].Values.IsDefault)
        {
            Report(output, resolver, "CborResolver requires a non-null array of closed root types.");
            return;
        }

        var roots = arguments[0].Values;
        var graph = new List<Node>();
        var pending = new Queue<ITypeSymbol>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        bool valid = true;
        foreach (var root in roots)
        {
            if (root.Value is ITypeSymbol type)
            {
                pending.Enqueue(type);
            }
            else
            {
                Report(output, resolver, "CborResolver root entries must be non-null closed types.");
                valid = false;
            }
        }

        while (pending.Count != 0)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            var type = pending.Dequeue();
            // Bound constructed types before symbol hashing or display formatting can
            // recursively traverse a large substituted argument expression.
            if (!IsClosedBoundedType(type, output))
            {
                valid = false;
                continue;
            }

            if (visited.Contains(type))
            {
                continue;
            }

            if (visited.Count == MaxGraphTypes)
            {
                Report(output, resolver, "The CBOR resolver graph exceeds 4096 distinct types. Split finite graphs into smaller resolvers; recursively expanding generic dependencies cannot be generated.");
                valid = false;
                break;
            }

            valid &= Visit(output, type, graph, visited, pending, compilation, resolver);
        }

        if (!valid)
        {
            return;
        }

        for (int i = 0; i < graph.Count; i++)
        {
            if (graph[i].Formatter is null && resolver.GetMembers("ObjectFormatter" + i.ToString(CultureInfo.InvariantCulture)).Length != 0)
            {
                Report(output, resolver, "A CBOR resolver contains a member whose name conflicts with a generated ObjectFormatter type.");
                return;
            }
        }

        var code = new StringBuilder("// <auto-generated/>\n#nullable enable\nusing Cbor;\nusing SerializerFoundation;\n");
        if (!resolver.ContainingNamespace.IsGlobalNamespace)
        {
            code.Append("namespace ").Append(resolver.ContainingNamespace.ToDisplayString()).Append(";\n");
        }

        code.Append(AccessibilityText(resolver.DeclaredAccessibility)).Append(" partial class @")
            .Append(resolver.Name).Append(" : global::Cbor.CborFormatterResolver\n{\n")
            .Append("    /// <summary>Shared generated resolver for the explicitly rooted type graph.</summary>\n")
            .Append("    public static ").Append(Name(resolver)).Append(" Instance { get; } = new ")
            .Append(Name(resolver)).Append("();\n")
            .Append("    protected override global::Cbor.CborFormatterFactory Provider { get; } = new GeneratedFactory();\n")
            .Append("    private sealed class GeneratedFactory : global::Cbor.CborFormatterFactory\n    {\n")
            .Append("        public override object? CreateFormatter(global::System.Type writeBufferType, global::System.Type readBufferType, global::System.Type valueType)\n")
            .Append("        {\n")
            .Append("            if (writeBufferType == typeof(global::SerializerFoundation.CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(global::SerializerFoundation.CompatibleReadOnlySpanReadBuffer)) return CreateFormatter<global::SerializerFoundation.CompatibleArrayPoolListWriteBuffer, global::SerializerFoundation.CompatibleReadOnlySpanReadBuffer>(valueType);\n")
            .Append("            if (writeBufferType == typeof(global::SerializerFoundation.CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(global::SerializerFoundation.CompatibleReadOnlySequenceReadBuffer)) return CreateFormatter<global::SerializerFoundation.CompatibleArrayPoolListWriteBuffer, global::SerializerFoundation.CompatibleReadOnlySequenceReadBuffer>(valueType);\n")
            .Append("            if (writeBufferType == typeof(global::SerializerFoundation.CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(global::SerializerFoundation.CompatibleReadOnlySpanReadBuffer)) return CreateFormatter<global::SerializerFoundation.CompatibleBufferWriterWriteBuffer, global::SerializerFoundation.CompatibleReadOnlySpanReadBuffer>(valueType);\n")
            .Append("            if (writeBufferType == typeof(global::SerializerFoundation.CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(global::SerializerFoundation.CompatibleReadOnlySequenceReadBuffer)) return CreateFormatter<global::SerializerFoundation.CompatibleBufferWriterWriteBuffer, global::SerializerFoundation.CompatibleReadOnlySequenceReadBuffer>(valueType);\n")
            .Append("            return null;\n        }\n")
            .Append("#if NET9_0_OR_GREATER\n        public override\n#else\n        public\n#endif\n")
            .Append("        object? CreateFormatter<W, R>(global::System.Type type)\n")
            .Append("#if !NET9_0_OR_GREATER\n            where W : struct, global::SerializerFoundation.IWriteBuffer\n            where R : struct, global::SerializerFoundation.IReadBuffer\n#endif\n        {\n");
        for (int i = 0; i < graph.Count; i++)
        {
            var node = graph[i];
            string formatter = node.Formatter is null ? "ObjectFormatter" + i.ToString(CultureInfo.InvariantCulture) + "<W, R>" :
                node.Formatter.Insert(node.Formatter.IndexOf('<') + 1, "W, R, ");
            code.Append("            if (type == typeof(").Append(Name(node.Type)).Append("))\n")
                .Append("                return new ").Append(formatter).Append("();\n");
        }

        code.Append("            return null;\n        }\n    }\n");
        for (int i = 0; i < graph.Count; i++)
        {
            if (graph[i].Formatter is null)
            {
                if (graph[i].Type.TypeKind == TypeKind.Enum)
                {
                    EmitEnum(code, (INamedTypeSymbol)graph[i].Type, i);
                }
                else
                {
                    EmitObject(code, graph[i], i);
                }
            }
        }

        code.Append("}\n");
        string hint = resolver.ToDisplayString() + ".Cbor.g.cs";
        output.AddSource(hint, SourceText.From(code.ToString(), Encoding.UTF8));
    }

    private static bool Visit(SourceProductionContext output, ITypeSymbol type, List<Node> graph, HashSet<ITypeSymbol> visited, Queue<ITypeSymbol> pending,
        Compilation compilation, INamedTypeSymbol resolver)
    {
        if (!visited.Add(type))
        {
            return true;
        }

        if (IsBuiltin(type) || (IsHalf(type) && compilation.GetTypeByMetadataName("Cbor.CborHalfFormatter`2") is not null))
        {
            return true;
        }

        if (type is IArrayTypeSymbol array && array.Rank == 1)
        {
            graph.Add(new Node(type, "global::Cbor.CborArrayFormatter<" + Name(array.ElementType) + ">"));
            pending.Enqueue(array.ElementType);
            return true;
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.TypeKind == TypeKind.Enum && Accessible(named, compilation, resolver))
            {
                graph.Add(new Node(type, null));
                return true;
            }

            string definition = named.OriginalDefinition.ToDisplayString();
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                graph.Add(new Node(type, "global::Cbor.CborNullableFormatter<" + Name(named.TypeArguments[0]) + ">"));
                pending.Enqueue(named.TypeArguments[0]);
                return true;
            }

            if (definition == "Cbor.CborTagged<T>")
            {
                graph.Add(new Node(type, "global::Cbor.CborTaggedFormatter<" + Name(named.TypeArguments[0]) + ">"));
                pending.Enqueue(named.TypeArguments[0]);
                return true;
            }

            if (definition is "System.Collections.Generic.List<T>" or "System.Collections.Generic.Dictionary<TKey, TValue>")
            {
                if (named.TypeArguments.Length == 2 && !IsDictionaryKey(named.TypeArguments[0]))
                {
                    output.ReportDiagnostic(Diagnostic.Create(UnsupportedType, LocationOf(named),
                        "Generated dictionaries require an integral, enum, Boolean, floating-point, or string key. Other keys need an explicitly registered formatter and comparer."));
                    return false;
                }

                string formatter = definition.StartsWith("System.Collections.Generic.List", StringComparison.Ordinal)
                    ? "global::Cbor.CborListFormatter<" : "global::Cbor.CborDictionaryFormatter<";
                graph.Add(new Node(type, formatter + string.Join(", ", named.TypeArguments.Select(Name)) + ">"));
                foreach (var argument in named.TypeArguments)
                {
                    pending.Enqueue(argument);
                }

                return true;
            }

            if (HasAttribute(named, "Cbor.CborObjectAttribute"))
            {
                var node = ReadObject(output, named, compilation, resolver);
                if (node is null)
                {
                    return false;
                }

                graph.Add(node);
                foreach (var member in node.Members)
                {
                    pending.Enqueue(member.Type);
                }

                return true;
            }
        }

        output.ReportDiagnostic(Diagnostic.Create(UnsupportedType, LocationOf(type),
            "No generated or built-in CBOR formatter exists for " + type.ToDisplayString() +
            ". Add an explicit CborObject contract or remove this type from the resolver graph."));
        return false;
    }

    private static bool IsClosedBoundedType(ITypeSymbol type, SourceProductionContext output)
    {
        if (IsBuiltin(type) || type is INamedTypeSymbol { Arity: 0, ContainingType: null, TypeKind: not TypeKind.Error })
        {
            return true;
        }

        var pending = new Stack<(ITypeSymbol Type, int Depth)>();
        pending.Push((type, 0));
        int components = 0;
        while (pending.Count != 0)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            var item = pending.Pop();
            if (++components > MaxTypeComponents)
            {
                Report(output, type, "CBOR type construction exceeds 4096 components. Recursively expanding generic dependencies cannot be generated.");
                return false;
            }

            if (item.Depth > MaxTypeNesting)
            {
                Report(output, type, "CBOR type construction exceeds 64 nesting levels. Recursively expanding generic dependencies cannot be generated.");
                return false;
            }

            if (item.Type.TypeKind is TypeKind.TypeParameter or TypeKind.Error ||
                item.Type is INamedTypeSymbol { IsUnboundGenericType: true })
            {
                Report(output, type, "A generated CBOR graph requires closed types; open generic definitions and unresolved type parameters are unsupported.");
                return false;
            }

            if (item.Type is IArrayTypeSymbol array)
            {
                pending.Push((array.ElementType, item.Depth + 1));
            }
            else if (item.Type is INamedTypeSymbol named)
            {
                foreach (var argument in named.TypeArguments)
                {
                    pending.Push((argument, item.Depth + 1));
                }

                if (named.ContainingType is not null)
                {
                    pending.Push((named.ContainingType, item.Depth + 1));
                }
            }
        }

        return true;
    }

    private static Node? ReadObject(SourceProductionContext output, INamedTypeSymbol type, Compilation compilation, INamedTypeSymbol resolver)
    {
        if (type.IsAbstract || type.IsRefLikeType ||
            type.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            !Accessible(type, compilation, resolver) ||
            type.DeclaringSyntaxReferences.Any(static reference => reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(static modifier => modifier.ValueText == "file")))
        {
            Report(output, type, "Generated CBOR objects must be accessible, concrete, closed classes or structs that are not ref-like or file-local.");
            return null;
        }

        var members = new List<Member>();
        var keys = new HashSet<int>();
        var slots = ReadContractSlots(output, type, out bool valid);
        foreach (var slot in slots)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            var symbol = slot.Symbol;
            if (symbol.IsImplicitlyDeclared || symbol is not (IPropertySymbol or IFieldSymbol))
            {
                continue;
            }

            var keyAttribute = slot.Key;
            if (symbol.IsStatic)
            {
                if (keyAttribute is not null)
                {
                    Report(output, symbol, "Static members cannot be part of an instance CBOR object contract.");
                    valid = false;
                }

                continue;
            }

            bool ignored = slot.Ignored;
            if (ignored && keyAttribute is not null)
            {
                Report(output, symbol, "A member cannot have both CborKey and CborIgnore.");
                valid = false;
                continue;
            }

            if (ignored)
            {
                continue;
            }

            if (keyAttribute is null)
            {
                if (symbol.DeclaredAccessibility == Accessibility.Public)
                {
                    Report(output, symbol, "Every public instance member of a CBOR object needs CborKey or CborIgnore.");
                    valid = false;
                }

                continue;
            }

            if (keyAttribute.ConstructorArguments.Length != 1 || keyAttribute.ConstructorArguments[0].Value is not int key ||
                key < 0 || !keys.Add(key))
            {
                Report(output, symbol, "CBOR member keys must be nonnegative and unique within the object.");
                valid = false;
                continue;
            }

            ITypeSymbol memberType;
            bool writable;
            if (symbol is IPropertySymbol property)
            {
                if (property.IsIndexer || property.GetMethod is null || !Accessible(property.GetMethod, compilation, resolver))
                {
                    Report(output, symbol, "A keyed property must have an accessible getter and cannot be an indexer.");
                    valid = false;
                    continue;
                }

                memberType = property.Type;
                writable = property.SetMethod is not null && Accessible(property.SetMethod, compilation, resolver);
            }
            else
            {
                var field = (IFieldSymbol)symbol;
                if (!Accessible(field, compilation, resolver))
                {
                    Report(output, symbol, "A keyed field must be accessible to the generated formatter.");
                    valid = false;
                    continue;
                }

                memberType = field.Type;
                writable = !field.IsReadOnly;
            }

            bool required = keyAttribute.NamedArguments.Any(static pair => pair.Key == "Required" && pair.Value.Value is true);
            members.Add(new Member(symbol.Name, memberType, key, required, writable));
        }

        if (!valid)
        {
            return null;
        }

        var marked = type.InstanceConstructors.Where(static ctor => HasAttribute(ctor, "Cbor.CborConstructorAttribute")).ToArray();
        if (marked.Length > 1)
        {
            Report(output, type, "Only one constructor can have CborConstructor.");
            return null;
        }

        var candidates = marked.Length == 1 ? marked :
            type.InstanceConstructors.OrderBy(static ctor => ctor.Parameters.Length).ToArray();
        IMethodSymbol? selected = null;
        foreach (var constructor in candidates)
        {
            if (!Accessible(constructor, compilation, resolver))
            {
                continue;
            }

            bool matches = constructor.Parameters.All(parameter => parameter.RefKind == RefKind.None && members.Count(member =>
                string.Equals(member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                SymbolEqualityComparer.Default.Equals(member.Type, parameter.Type)) == 1);
            if (!matches)
            {
                continue;
            }

            if (members.Any(member => !member.Writable && !constructor.Parameters.Any(parameter =>
                string.Equals(member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            selected = constructor;
            break;
        }

        if (selected is null)
        {
            Report(output, type, "No accessible constructor binds every readonly keyed member. Constructor parameters must match keyed member names and types.");
            return null;
        }

        if (!HasAttribute(selected, "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
        {
            foreach (var symbol in slots.Select(static slot => slot.Symbol).Where(IsClrRequired))
            {
                var member = members.FirstOrDefault(member => member.Name == symbol.Name);
                bool bound = selected.Parameters.Any(parameter =>
                    string.Equals(symbol.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (member is null || !member.Writable || bound)
                {
                    Report(output, symbol, "A C# required member that is ignored or constructor-bound needs a constructor marked SetsRequiredMembers.");
                    return null;
                }
            }
        }

        members.Sort(static (left, right) => left.Key.CompareTo(right.Key));
        return new Node(type, null, members.ToImmutableArray(), selected);
    }

    private static List<ContractSlot> ReadContractSlots(SourceProductionContext output, INamedTypeSymbol type, out bool valid)
    {
        valid = true;
        var hierarchy = new Stack<INamedTypeSymbol>();
        var ancestors = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        for (var current = type; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); current = current.BaseType)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            if (!ancestors.Add(current))
            {
                Report(output, type, "A CBOR model hierarchy cannot contain an inheritance cycle.");
                valid = false;
                break;
            }

            if (!HasAttribute(current, "Cbor.CborObjectAttribute"))
            {
                Report(output, type, "Every model base class must explicitly declare CborObject; framework base classes are not inferred as wire contracts.");
                valid = false;
            }

            hierarchy.Push(current);
        }

        var slots = new List<ContractSlot>();
        var byName = new Dictionary<string, int>(StringComparer.Ordinal);
        while (hierarchy.Count != 0)
        {
            var current = hierarchy.Pop();
            foreach (var symbol in current.GetMembers())
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                if (symbol.IsImplicitlyDeclared)
                {
                    continue;
                }

                bool existing = byName.TryGetValue(symbol.Name, out int index);
                var inherited = existing ? slots[index] : null;
                if (symbol is not (IPropertySymbol or IFieldSymbol))
                {
                    if (inherited?.Key is not null)
                    {
                        Report(output, symbol, "A member cannot hide an inherited keyed CBOR member.");
                        valid = false;
                    }

                    continue;
                }

                var key = Attribute(symbol, "Cbor.CborKeyAttribute");
                bool ignored = HasAttribute(symbol, "Cbor.CborIgnoreAttribute");
                if (symbol.IsStatic)
                {
                    if (inherited?.Key is not null)
                    {
                        Report(output, symbol, "A static member cannot hide an inherited keyed CBOR member.");
                        valid = false;
                    }

                    slots.Add(new ContractSlot(symbol, key, ignored));
                    continue;
                }

                if (inherited is not null && symbol is IPropertySymbol { OverriddenProperty: not null } property &&
                    SymbolEqualityComparer.Default.Equals(property.OverriddenProperty, inherited.Symbol))
                {
                    if (inherited.Key is not null && (ignored || (key is not null && !SameKeyContract(key, inherited.Key))))
                    {
                        Report(output, symbol, "A CBOR property override must preserve its inherited key and Required contract, and cannot ignore a keyed slot.");
                        valid = false;
                    }

                    key ??= inherited.Key;
                    ignored |= key is null && inherited.Ignored;
                }
                else if (inherited?.Key is not null)
                {
                    Report(output, symbol, "A member cannot hide an inherited keyed CBOR member; use a property override that preserves the slot contract.");
                    valid = false;
                }

                if (!ignored && key is null && symbol.DeclaredAccessibility == Accessibility.Public)
                {
                    Report(output, symbol, "Every public instance member of a CBOR object needs CborKey or CborIgnore.");
                    valid = false;
                }

                if (key is not null && (ignored || key.ConstructorArguments.Length != 1 ||
                    key.ConstructorArguments[0].Value is not int memberKey || memberKey < 0))
                {
                    Report(output, symbol, "CBOR member keys must be nonnegative, and a keyed member cannot have CborIgnore.");
                    valid = false;
                }

                var slot = new ContractSlot(symbol, key, ignored);
                if (existing)
                {
                    slots[index] = slot;
                }
                else
                {
                    byName.Add(symbol.Name, slots.Count);
                    slots.Add(slot);
                }
            }
        }

        return slots;
    }

    private static bool SameKeyContract(AttributeData left, AttributeData right) =>
        left.ConstructorArguments.Length == 1 && right.ConstructorArguments.Length == 1 &&
        Equals(left.ConstructorArguments[0].Value, right.ConstructorArguments[0].Value) &&
        left.NamedArguments.Any(static pair => pair.Key == "Required" && pair.Value.Value is true) ==
        right.NamedArguments.Any(static pair => pair.Key == "Required" && pair.Value.Value is true);

    private static void EmitEnum(StringBuilder code, INamedTypeSymbol type, int index)
    {
        string model = Name(type);
        string underlying = Name(type.EnumUnderlyingType!);
        code.Append("    private sealed class ObjectFormatter").Append(index.ToString(CultureInfo.InvariantCulture));
        EmitFormatterBase(code, model);
        EmitFormatterFields(code, [type.EnumUnderlyingType!]);
        EmitSerializeSignature(code, model);
        code.Append("            => context.SerializeSameItem(ref buffer, (").Append(underlying)
            .Append(")value, formatter0);\n");
        EmitDeserializeSignature(code, model);
        code.Append("            => (").Append(model).Append(")context.DeserializeSameItem(ref buffer, formatter0);\n    }\n");
    }

    private static void EmitObject(StringBuilder code, Node node, int index)
    {
        string model = Name(node.Type);
        bool reference = node.Type.IsReferenceType;
        var formatterTypes = new List<ITypeSymbol>();
        var formatterTypeIndices = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        var formatterIndices = new int[node.Members.Length];
        for (int i = 0; i < node.Members.Length; i++)
        {
            var type = node.Members[i].Type;
            if (!formatterTypeIndices.TryGetValue(type, out int formatterIndex))
            {
                formatterIndex = formatterTypes.Count;
                formatterTypes.Add(type);
                formatterTypeIndices.Add(type, formatterIndex);
            }

            formatterIndices[i] = formatterIndex;
        }

        code.Append("    private sealed class ObjectFormatter").Append(index.ToString(CultureInfo.InvariantCulture));
        EmitFormatterBase(code, model);
        EmitFormatterFields(code, formatterTypes);
        EmitSerializeSignature(code, model);
        code.Append("        {\n");
        if (reference)
        {
            code.Append("            if (value is null) { buffer.WriteNull(); return; }\n");
        }

        code.Append("            context.CheckCollectionLength(").Append(node.Members.Length.ToString(CultureInfo.InvariantCulture)).Append(");\n")
            .Append("            context.EnterContainer();\n            try\n            {\n")
            .Append("                buffer.WriteMapHeader(").Append(node.Members.Length.ToString(CultureInfo.InvariantCulture)).Append("UL);\n");
        for (int i = 0; i < node.Members.Length; i++)
        {
            var member = node.Members[i];
            code.Append("                context.WriteObjectKey(ref buffer, ").Append(member.Key.ToString(CultureInfo.InvariantCulture)).Append(");\n")
                .Append("                context.Serialize<W, R, ").Append(Name(member.Type)).Append(">(ref buffer, value.@")
                .Append(member.Name).Append("!, formatter")
                .Append(formatterIndices[i].ToString(CultureInfo.InvariantCulture)).Append(");\n");
        }

        code.Append("            }\n            finally { context.ExitContainer(); }\n        }\n");
        EmitDeserializeSignature(code, model);
        code.Append("        {\n");
        if (reference)
        {
            code.Append("            if (global::Cbor.CborDeserializationContext.TryReadNull(ref buffer)) return null!;\n");
        }

        for (int i = 0; i < node.Members.Length; i++)
        {
            code.Append("            ").Append(Name(node.Members[i].Type)).Append(" value").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" = default!;\n")
                .Append("            bool seen").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" = false;\n");
        }

        code.Append("            context.EnterContainer();\n            try\n            {\n")
            .Append("                int? length = context.ReadMapLength(ref buffer);\n")
            .Append("                for (int i = 0; !length.HasValue || i < length.Value; i++)\n                {\n")
            .Append("                    if (!length.HasValue && buffer.TryReadBreak()) break;\n")
            .Append("                    context.CheckCollectionLength(i + 1);\n")
            .Append("                    int key = context.ReadObjectKey(ref buffer);\n")
            .Append("                    switch (key)\n                    {\n");
        for (int i = 0; i < node.Members.Length; i++)
        {
            var member = node.Members[i];
            string local = i.ToString(CultureInfo.InvariantCulture);
            code.Append("                        case ").Append(member.Key.ToString(CultureInfo.InvariantCulture)).Append(":\n")
                .Append("                            if (seen").Append(local).Append(") throw new global::System.IO.InvalidDataException(\"Duplicate CBOR object member key.\");\n")
                .Append("                            seen").Append(local).Append(" = true;\n")
                .Append("                            value").Append(local).Append(" = context.Deserialize<W, R, ").Append(Name(member.Type))
                .Append(">(ref buffer, formatter").Append(formatterIndices[i].ToString(CultureInfo.InvariantCulture))
                .Append(");\n")
                .Append("                            break;\n");
        }

        code.Append("                        default: context.SkipValue(ref buffer); break;\n")
            .Append("                    }\n                }\n");
        for (int i = 0; i < node.Members.Length; i++)
        {
            if (node.Members[i].Required)
            {
                code.Append("                if (!seen").Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append(") throw new global::System.IO.InvalidDataException(\"A required CBOR object member is missing.\");\n");
            }
        }

        code.Append("                return new ").Append(model).Append('(');
        var parameters = node.Constructor!.Parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i != 0)
            {
                code.Append(", ");
            }

            int memberIndex = -1;
            for (int j = 0; j < node.Members.Length; j++)
            {
                if (string.Equals(node.Members[j].Name, parameters[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    memberIndex = j;
                    break;
                }
            }

            code.Append("value").Append(memberIndex.ToString(CultureInfo.InvariantCulture));
        }

        code.Append(")\n                {\n");
        for (int i = 0; i < node.Members.Length; i++)
        {
            if (node.Members[i].Writable && !parameters.Any(parameter =>
                string.Equals(node.Members[i].Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                code.Append("                    @").Append(node.Members[i].Name).Append(" = value")
                    .Append(i.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            }
        }

        code.Append("                };\n            }\n            finally { context.ExitContainer(); }\n        }\n    }\n");
    }

    private static void EmitFormatterFields(StringBuilder code, List<ITypeSymbol> types)
    {
        for (int i = 0; i < types.Count; i++)
        {
            code.Append("        private global::Cbor.ICborFormatter<W, R, ").Append(Name(types[i])).Append("> formatter")
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(" = null!;\n");
        }
        code.Append("        public void Initialize(global::Cbor.CborFormatterResolver resolver)\n        {\n");
        for (int i = 0; i < types.Count; i++)
        {
            code.Append("            formatter").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" = resolver.GetFormatter<W, R, ")
                .Append(Name(types[i])).Append(">();\n");
        }
        code.Append("        }\n");
    }

    private static void EmitFormatterBase(StringBuilder code, string model) => code
        .Append("<W, R> : global::Cbor.ICborFormatter<W, R, ").Append(model).Append(">\n")
        .Append("        where W : struct, global::SerializerFoundation.IWriteBuffer\n#if NET9_0_OR_GREATER\n        , allows ref struct\n#endif\n")
        .Append("        where R : struct, global::SerializerFoundation.IReadBuffer\n#if NET9_0_OR_GREATER\n        , allows ref struct\n#endif\n    {\n");

    private static void EmitSerializeSignature(StringBuilder code, string model) => code
        .Append("        public void Serialize(ref W buffer, ref global::Cbor.CborSerializationContext context, ")
        .Append(model).Append(" value)\n");

    private static void EmitDeserializeSignature(StringBuilder code, string model) => code
        .Append("        public ").Append(model)
        .Append(" Deserialize(ref R buffer, ref global::Cbor.CborDeserializationContext context)\n");

    private static bool IsBuiltin(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_String ||
        type is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte } ||
        type is INamedTypeSymbol { Arity: 0, ContainingType: null } named &&
        ((named.Name is "CborInteger" or "CborSimpleValue" && named.ContainingNamespace.ToDisplayString() == "Cbor") ||
         (named.Name == "BigInteger" && named.ContainingNamespace.ToDisplayString() == "System.Numerics"));

    private static bool IsHalf(ITypeSymbol type) => type is INamedTypeSymbol { Name: "Half", Arity: 0, ContainingType: null } named &&
        named.ContainingNamespace.ToDisplayString() == "System";

    private static bool Accessible(ISymbol symbol, Compilation compilation, INamedTypeSymbol resolver) =>
        compilation.IsSymbolAccessibleWithin(symbol, resolver);

    private static bool IsDictionaryKey(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Enum || type.SpecialType is
            SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_String;

    private static bool IsClrRequired(ISymbol symbol) =>
        HasAttribute(symbol, "System.Runtime.CompilerServices.RequiredMemberAttribute") ||
        symbol.DeclaringSyntaxReferences.Any(static reference =>
        {
            var syntax = reference.GetSyntax();
            if (syntax is PropertyDeclarationSyntax property)
            {
                return property.Modifiers.Any(static modifier => modifier.ValueText == "required");
            }

            var field = syntax.AncestorsAndSelf().OfType<BaseFieldDeclarationSyntax>().FirstOrDefault();
            return field is not null && field.Modifiers.Any(static modifier => modifier.ValueText == "required");
        });

    private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static AttributeData? Attribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == name);
    private static bool HasAttribute(ISymbol symbol, string name) => Attribute(symbol, name) is not null;
    private static Location LocationOf(ISymbol symbol) => symbol.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;
    private static string AccessibilityText(Accessibility accessibility) => accessibility == Accessibility.Public ? "public" : "internal";
    private static void Report(SourceProductionContext output, ISymbol symbol, string message) =>
        output.ReportDiagnostic(Diagnostic.Create(InvalidContract, LocationOf(symbol), message));

    private sealed class Node(ITypeSymbol type, string? formatter, ImmutableArray<Member> members = default, IMethodSymbol? constructor = null)
    {
        internal ITypeSymbol Type { get; } = type;
        internal string? Formatter { get; } = formatter;
        internal ImmutableArray<Member> Members { get; } = members;
        internal IMethodSymbol? Constructor { get; } = constructor;
    }

    private sealed class Member(string name, ITypeSymbol type, int key, bool required, bool writable)
    {
        internal string Name { get; } = name;
        internal ITypeSymbol Type { get; } = type;
        internal int Key { get; } = key;
        internal bool Required { get; } = required;
        internal bool Writable { get; } = writable;
    }

    private sealed class ContractSlot(ISymbol symbol, AttributeData? key, bool ignored)
    {
        internal ISymbol Symbol { get; } = symbol;
        internal AttributeData? Key { get; } = key;
        internal bool Ignored { get; } = ignored;
    }
}
