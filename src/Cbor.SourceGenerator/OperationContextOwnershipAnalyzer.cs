// Adapted from SerializerFoundation NonCopyableBufferAnalyzer.
// Copyright (c) 2026 Cysharp, Inc. MIT license: see THIRD-PARTY-NOTICES.txt.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Cbor.SourceGenerator;

/// <summary>Rejects ownership copies of operation budgets and pooled formatter tables.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OperationContextOwnershipAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic for a copy of single-owner operation state.</summary>
    public const string DiagnosticId = "CBOR003";

    private static readonly string[] ContextMetadataNames =
        ["Cbor.CborSerializationContext", "Cbor.CborDeserializationContext"];

    // Roslyn 4.8 spells this RefKind.RefReadOnlyParameter; the analyzer floor is 4.3.1 (Unity 2022.3 / Unity 6
    // ship Roslyn 4.3), where the enum member does not exist yet but the value is the same
    const RefKind RefReadOnlyParameter = (RefKind)4;

    // ILocalSymbol.IsUsing is Roslyn 4.4+; the syntax answers the same question on 4.3
    static bool IsUsingLocal(ILocalSymbol local)
    {
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            for (var node = reference.GetSyntax().Parent; node is not null; node = node.Parent)
            {
                switch (node)
                {
                    case LocalDeclarationStatementSyntax declaration:
                        return declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);
                    case UsingStatementSyntax:
                        return true;
                    case StatementSyntax:
                        return false;
                }
            }
        }
        return false;
    }

    static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Context structs are single-owner and must not be copied",
        "'{0}' must not be {1}; operation contexts are single-owner - pass by ref or construct in place",
        "Cbor",
        DiagnosticSeverity.Error, // this must not allow silent divergence of mutable state, so it's an error, not a warning
        isEnabledByDefault: true,
        description: "Operation contexts hold single-owner mutable state (budgets and rented formatter tables). A copy diverges silently and double-disposes pooled state. Pass contexts by ref (the formatter contract), construct them in place, and never box them. `in` and `ref readonly` parameters count as copies: the context's mutating members act on a defensive copy, and so does any non-readonly member called through a readonly field or a ref readonly reference.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var contextTypes = ImmutableArray.CreateRange(
                ContextMetadataNames
                .Select(startContext.Compilation.GetTypeByMetadataName)
                .Where(static symbol => symbol is not null)
                .Select(static symbol => symbol!));
            if (contextTypes.IsEmpty)
            {
                return;
            }

            startContext.RegisterOperationAction(
                context => AnalyzeAssignment(context, contextTypes),
                OperationKind.SimpleAssignment,
                OperationKind.VariableDeclarator,
                OperationKind.FieldInitializer);
            startContext.RegisterOperationAction(
                context => AnalyzeArgument(context, contextTypes),
                OperationKind.Argument);
            startContext.RegisterOperationAction(
                context => AnalyzeConversion(context, contextTypes),
                OperationKind.Conversion);
            startContext.RegisterOperationAction(
                context => AnalyzeParameters(context, ((ILocalFunctionOperation)context.Operation).Symbol, contextTypes),
                OperationKind.LocalFunction);
            startContext.RegisterOperationAction(
                context => AnalyzeParameters(context, ((IAnonymousFunctionOperation)context.Operation).Symbol, contextTypes),
                OperationKind.AnonymousFunction);
            startContext.RegisterSymbolAction(
                context => AnalyzeParameters(context, (IMethodSymbol)context.Symbol, contextTypes),
                SymbolKind.Method);
            startContext.RegisterSyntaxNodeAction(
                context => AnalyzeExtensionReceiver(context, contextTypes),
                SyntaxKind.Parameter);
            // delegate Invoke is implicitly declared, so the Method symbol action never
            // sees it; the delegate TYPE is the source-declared symbol to hook
            startContext.RegisterSymbolAction(
                context =>
                {
                    if (((INamedTypeSymbol)context.Symbol).DelegateInvokeMethod is { } invoke)
                    {
                        AnalyzeParameters(context, invoke, contextTypes);
                    }
                },
                SymbolKind.NamedType);
            startContext.RegisterSymbolAction(
                context => AnalyzeProperty(context, contextTypes),
                SymbolKind.Property);
            startContext.RegisterOperationAction(
                context => AnalyzeReturn(context, contextTypes),
                OperationKind.Return,
                OperationKind.YieldReturn);
            startContext.RegisterOperationAction(
                context => AnalyzeForEach(context, contextTypes),
                OperationKind.Loop);
            startContext.RegisterOperationAction(
                context => AnalyzePattern(context, contextTypes),
                OperationKind.DeclarationPattern,
                OperationKind.RecursivePattern);
            startContext.RegisterOperationAction(
                context => AnalyzeWith(context, contextTypes),
                OperationKind.With);
            startContext.RegisterOperationAction(
                context => AnalyzeTuple(context, contextTypes),
                OperationKind.Tuple);
            // New hosts understand this syntax. Bind its kind at runtime so the analyzer
            // still loads on the Roslyn 4.3 floor without newer operation interfaces.
            if (Enum.TryParse("CollectionExpression", out SyntaxKind collectionKind))
            {
                startContext.RegisterSyntaxNodeAction(
                    context => AnalyzeCollectionExpression(context, contextTypes), collectionKind);
            }

            startContext.RegisterOperationAction(
                context => AnalyzeCollectionElements(context, ((IArrayInitializerOperation)context.Operation).ElementValues, contextTypes),
                OperationKind.ArrayInitializer);
            startContext.RegisterOperationAction(
                context => AnalyzeUsing(context, contextTypes),
                OperationKind.Using);
            startContext.RegisterOperationAction(
                context => AnalyzeReadOnlyReceiver(context, contextTypes),
                OperationKind.Invocation);
        });
    }

    static void AnalyzeAssignment(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var (target, value) = context.Operation switch
        {
            // `r = ref b` and `ref var r = ref b` rebind a reference, no value moves
            ISimpleAssignmentOperation { IsRef: false } assignment => ((ITypeSymbol?)assignment.Target.Type, assignment.Value),
            IVariableDeclaratorOperation { Symbol.IsRef: false, Initializer.Value: { } initializer } declarator => (declarator.Symbol.Type, initializer),
            IFieldInitializerOperation field when !field.InitializedFields.IsEmpty && field.InitializedFields[0].RefKind == RefKind.None
                => (field.InitializedFields[0].Type, field.Value),
            _ => (null, null),
        };
        if (target is null || value is null || !IsContext(target, contextTypes) || IsFreshValue(value))
        {
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), target.Name, "copied by assignment"));
    }

    static void AnalyzeArgument(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (argument.Parameter is not { } parameter || argument.Value.Type is not { } type || !IsContext(type, contextTypes))
        {
            return;
        }
        if (parameter.RefKind is RefKind.In or RefReadOnlyParameter)
        {
            // no copy at the call site, but the callee cannot mutate through a readonly reference:
            // every mutating member call inside acts on a hidden defensive copy, so the
            // caller's context never advances (flagged even for a fresh value, the callee is wrong either way)
            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Syntax.GetLocation(), type.Name, "passed as a readonly reference (the callee's in/ref readonly parameter mutates a defensive copy)"));
            return;
        }
        if (parameter.RefKind != RefKind.None || IsFreshValue(argument.Value))
        {
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Syntax.GetLocation(), type.Name, "passed by value"));
    }

    static void AnalyzeConversion(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (conversion.Operand.Type is not { } operandType || !IsContext(operandType, contextTypes))
        {
            return;
        }
        if (conversion.Type is { IsReferenceType: true })
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Syntax.GetLocation(), operandType.Name, "boxed"));
        }
        else if (conversion.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            // not a box, but the same escape: the wrapper is not a context type, so the
            // copy inside it dodges every other rule from here on
            context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Syntax.GetLocation(), operandType.Name, "wrapped in a Nullable"));
        }
    }

    static void AnalyzeParameters(DiagnosticReporter context, IMethodSymbol method, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation or MethodKind.LocalFunction or MethodKind.Constructor
            or MethodKind.AnonymousFunction or MethodKind.DelegateInvoke or MethodKind.UserDefinedOperator or MethodKind.Conversion))
        {
            return;
        }
        foreach (var parameter in method.Parameters)
        {
            AnalyzeParameter(context, parameter, contextTypes);
        }
    }

    static void AnalyzeExtensionReceiver(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        // A C# 14 extension receiver belongs to the extension's named type, not to
        // any member's parameter list. ParameterSyntax and GetDeclaredSymbol work
        // on both old and new hosts without taking a dependency on Roslyn 5 APIs.
        if (context.SemanticModel.GetDeclaredSymbol((ParameterSyntax)context.Node, context.CancellationToken)
            is IParameterSymbol { ContainingSymbol: INamedTypeSymbol extension } parameter &&
            extension.GetMembers().Any(static member => !member.IsStatic))
        {
            // Static extension members do not receive an instance or copy the context.
            AnalyzeParameter(context, parameter, contextTypes);
        }
    }

    static void AnalyzeParameter(DiagnosticReporter context, IParameterSymbol parameter, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        // ref and out are the only sanctioned shapes: a context's members are mutating, so an
        // in / ref readonly parameter acts on a hidden defensive copy when mutated.
        if (parameter.RefKind is not (RefKind.None or RefKind.In or RefReadOnlyParameter) || !IsContext(parameter.Type, contextTypes))
        {
            return;
        }
        var location = parameter.Locations.IsDefaultOrEmpty ? Location.None : parameter.Locations[0];
        var shape = parameter.RefKind == RefKind.None
            ? "received by value (declare the parameter ref)"
            : "received as a readonly reference (in/ref readonly: every mutating call acts on a hidden defensive copy; declare the parameter ref)";
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, parameter.Type.Name, shape));
    }

    // the getter of a by-value property hands out a COPY on every access, so
    // `x.Context.Dispose()` silently disposes a temporary — no other rule can see that,
    // because no assignment/argument/conversion is involved at the use site
    static void AnalyzeProperty(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.RefKind != RefKind.None || !IsContext(property.Type, contextTypes))
        {
            return;
        }
        var location = property.Locations.IsDefaultOrEmpty ? Location.None : property.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, property.Type.Name, "exposed as a by-value property (use a ref-returning property or a field)"));
    }

    // returning a LOCAL or a fresh value is the factory move (the storage dies with the
    // frame); returning a field, a ref target, or a by-ref parameter duplicates storage
    // that stays alive behind the caller's back. Ref-returning members hand out the
    // storage itself and are exempt. A `using` local is the exception among locals: the
    // frame disposes it on exit, so the caller receives a copy of a dead context (and the
    // pooled state behind it is returned twice).
    static void AnalyzeReturn(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var returnOperation = (IReturnOperation)context.Operation;
        if (returnOperation.ReturnedValue is not { Type: { } type } value ||
            !IsContext(type, contextTypes) ||
            context.ContainingSymbol is IMethodSymbol { RefKind: not RefKind.None })
        {
            return;
        }
        var unwrapped = Unwrap(value);
        // An iterator suspends rather than exiting: its locals remain alive after yield,
        // so only a fresh value transfers ownership without duplicating retained storage.
        if (returnOperation.Kind == OperationKind.YieldReturn)
        {
            if (!IsFreshValue(unwrapped))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), type.Name, "yielded by value from existing storage (the iterator retains ownership; yield a fresh context)"));
            }
            return;
        }
        if (unwrapped is ILocalReferenceOperation { Local: { IsRef: false } usingCandidate } && IsUsingLocal(usingCandidate))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), type.Name, "returned from a using local (the local is disposed when the method exits, so the caller gets a copy of a disposed context; drop the using and let the caller own it)"));
            return;
        }
        if (IsFreshValue(unwrapped) ||
            unwrapped is ILocalReferenceOperation { Local.IsRef: false } ||
            unwrapped is IParameterReferenceOperation { Parameter.RefKind: RefKind.None }) // the by-value parameter was already diagnosed at its declaration
        {
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), type.Name, "returned by value from existing storage (return a fresh value or return by ref)"));
    }

    static void AnalyzeForEach(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        if (context.Operation is not IForEachLoopOperation { LoopControlVariable: IVariableDeclaratorOperation { Symbol: { } local } })
        {
            return;
        }
        if (local.RefKind != RefKind.None || !IsContext(local.Type, contextTypes))
        {
            return;
        }
        var location = local.Locations.IsDefaultOrEmpty ? context.Operation.Syntax.GetLocation() : local.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, local.Type.Name, "copied by foreach (iterate by ref, or index the collection)"));
    }

    static void AnalyzePattern(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var declared = context.Operation switch
        {
            IDeclarationPatternOperation pattern => pattern.DeclaredSymbol as ILocalSymbol,
            IRecursivePatternOperation pattern => pattern.DeclaredSymbol as ILocalSymbol,
            _ => null,
        };
        if (declared is null || !IsContext(declared.Type, contextTypes))
        {
            return;
        }
        var location = declared.Locations.IsDefaultOrEmpty ? context.Operation.Syntax.GetLocation() : declared.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, declared.Type.Name, "copied by a pattern match"));
    }

    static void AnalyzeWith(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var with = (IWithOperation)context.Operation;
        if (with.Operand.Type is { } type && IsContext(type, contextTypes))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, with.Syntax.GetLocation(), type.Name, "copied by a with-expression"));
        }
    }

    // even a FRESH context is flagged here: the tuple itself is not a context type, so
    // every subsequent tuple copy would duplicate the context invisibly to all rules
    static void AnalyzeTuple(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var tuple = (ITupleOperation)context.Operation;
        foreach (var element in tuple.Elements)
        {
            if (Unwrap(element).Type is { } type && IsContext(type, contextTypes))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, element.Syntax.GetLocation(), type.Name, "captured in a tuple (tuples copy freely and hide the context)"));
            }
        }
    }

    static void AnalyzeCollectionElements(OperationAnalysisContext context, IEnumerable<IOperation> elements, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        foreach (var element in elements)
        {
            var unwrapped = Unwrap(element);
            if (unwrapped.Type is { } type && IsContext(type, contextTypes) && !IsFreshValue(unwrapped))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, element.Syntax.GetLocation(), type.Name, "copied into a collection"));
            }
        }
    }

    static void AnalyzeCollectionExpression(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var target = context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).ConvertedType;
        bool holdsContexts = target is IArrayTypeSymbol array && IsContext(array.ElementType, contextTypes) ||
            target is INamedTypeSymbol named && named.TypeArguments.Any(type => IsContext(type, contextTypes));
        foreach (var element in context.Node.ChildNodes())
        {
            var expression = element.ChildNodes().FirstOrDefault();
            if (expression is null)
            {
                continue;
            }

            if (element.Kind().ToString() == "SpreadElement")
            {
                if (holdsContexts)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, element.GetLocation(), target!.Name,
                        "copied by a collection spread"));
                }

                continue;
            }

            var value = context.SemanticModel.GetOperation(expression, context.CancellationToken);
            if (value is not null && Unwrap(value).Type is { } type && IsContext(type, contextTypes) && !IsFreshValue(value))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation(), type.Name,
                    "copied into a collection"));
            }
        }
    }

    // `using (a)` copies the resource into a hidden slot and disposes THAT; the
    // declaration form (`using var a = ...;`) is handled by the assignment rule
    static void AnalyzeUsing(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var usingOperation = (IUsingOperation)context.Operation;
        if (usingOperation.Resources is IVariableDeclarationGroupOperation)
        {
            return;
        }
        var resource = Unwrap(usingOperation.Resources);
        if (resource.Type is { } type && IsContext(type, contextTypes) && !IsFreshValue(resource))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, resource.Syntax.GetLocation(), type.Name, "copied by a using statement (declare the resource inside the using)"));
        }
    }

    // a context reached through a READONLY reference (readonly field, `ref readonly`
    // local/return, an `in` path through a containing struct, `this` inside a readonly
    // member) cannot be mutated in place: the compiler runs every non-readonly member on
    // a hidden defensive copy, so budgets and pool ownership diverge from the real
    // context. The `in`-parameter form is already reported at its declaration.
    static void AnalyzeReadOnlyReceiver(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.IsStatic ||
            invocation.TargetMethod.IsReadOnly ||
            invocation.Instance is not { Type: { } receiverType } receiver ||
            !IsContext(receiverType, contextTypes) ||
            receiver is IParameterReferenceOperation ||
            !IsReadOnlyReference(receiver, context.ContainingSymbol))
        {
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(Rule, receiver.Syntax.GetLocation(), receiverType.Name, $"mutated through a readonly reference (the non-readonly member '{invocation.TargetMethod.Name}' runs on a hidden defensive copy; reach the context through a ref or a mutable field)"));
    }

    static bool IsReadOnlyReference(IOperation reference, ISymbol containingSymbol)
    {
        switch (reference)
        {
            case IFieldReferenceOperation field:
                if (field.Field.IsReadOnly)
                {
                    // A readonly field is writable in its declaring constructor, but only
                    // on this (or statically), never on another instance or in a closure.
                    return !IsWritableInConstructor(field, containingSymbol);
                }
                // a mutable field of a struct is still readonly when the struct itself is
                // reached through a readonly reference (h.b with `in Holder h`)
                return field.Field.ContainingType.IsValueType && field.Instance is { } instance && IsReadOnlyReference(instance, containingSymbol);
            case ILocalReferenceOperation local:
                return local.Local.RefKind == RefKind.RefReadOnly;
            case IParameterReferenceOperation parameter:
                return parameter.Parameter.RefKind is RefKind.In or RefReadOnlyParameter;
            case IInvocationOperation call:
                return call.TargetMethod.RefKind == RefKind.RefReadOnly;
            case IPropertyReferenceOperation property:
                return property.Property.RefKind == RefKind.RefReadOnly;
            case IInstanceReferenceOperation:
                // `this` inside a readonly member (or any member of a readonly struct)
                return containingSymbol is IMethodSymbol { IsReadOnly: true } || containingSymbol.ContainingType is { IsReadOnly: true };
            default:
                return false;
        }
    }

    static bool IsWritableInConstructor(IFieldReferenceOperation field, ISymbol containingSymbol)
    {
        if (containingSymbol is not IMethodSymbol method ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, field.Field.ContainingType))
        {
            return false;
        }
        // Operation callbacks keep the outer block's containing symbol even inside
        // lambdas/local functions, which do not inherit the constructor's write access.
        for (var parent = field.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return false;
            }
        }
        return field.Field.IsStatic
            ? method.MethodKind == MethodKind.StaticConstructor
            : method.MethodKind == MethodKind.Constructor &&
              field.Instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };
    }

    static IOperation Unwrap(IOperation value)
    {
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }
        return value;
    }

    // fresh values transfer ownership (move, not copy): construction, default, and
    // by-value method returns; everything read from an existing storage location is a copy.
    // A ref-returning method hands out existing storage, so receiving its result by value
    // dereferences and copies, same as reading a field.
    static bool IsFreshValue(IOperation value)
    {
        return Unwrap(value) switch
        {
            IObjectCreationOperation or IDefaultValueOperation or ILiteralOperation => true,
            IInvocationOperation invocation => invocation.TargetMethod.RefKind == RefKind.None,
            IConditionalOperation { WhenFalse: { } whenFalse } conditional => IsFreshValue(conditional.WhenTrue) && IsFreshValue(whenFalse),
            ISwitchExpressionOperation switchExpression => switchExpression.Arms.All(static arm => IsFreshValue(arm.Value)),
            _ => false,
        };
    }

    static bool IsContext(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> contextTypes)
    {
        foreach (var contextType in contextTypes)
        {
            if (SymbolEqualityComparer.Default.Equals(type, contextType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adapter so parameter checks share code between symbol, syntax and operation contexts.</summary>
    readonly struct DiagnosticReporter
    {
        readonly Action<Diagnostic> report;

        DiagnosticReporter(Action<Diagnostic> report) => this.report = report;

        public void ReportDiagnostic(Diagnostic diagnostic) => report(diagnostic);

        public static implicit operator DiagnosticReporter(SymbolAnalysisContext context) => new(context.ReportDiagnostic);

        public static implicit operator DiagnosticReporter(OperationAnalysisContext context) => new(context.ReportDiagnostic);

        public static implicit operator DiagnosticReporter(SyntaxNodeAnalysisContext context) => new(context.ReportDiagnostic);
    }
}
