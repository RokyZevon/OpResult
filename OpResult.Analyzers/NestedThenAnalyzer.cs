using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpResult.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NestedThenAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NestedThen);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var compilation = startContext.Compilation;
            var extensions = compilation.GetTypesByMetadataName("OpResult.OpResultExtensions")
                .Where(type => type.ContainingAssembly.Name == "OpResult"
                    && !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
                .ToArray();
            if (extensions.Length == 1)
            {
                startContext.RegisterOperationBlockAction(blockContext => AnalyzeBlock(blockContext, extensions[0]));
            }
        });
    }

    private static void AnalyzeBlock(OperationBlockAnalysisContext context, INamedTypeSymbol extensions)
    {
        var pending = new Stack<(IOperation Operation, IInvocationOperation? Owner)>();
        var returnedChains = new Stack<IOperation>();
        var parents = new HashSet<IInvocationOperation>();
        var children = new HashSet<IInvocationOperation>();
        var cancellationToken = context.CancellationToken;

        foreach (var block in context.OperationBlocks)
        {
            pending.Push((block, null));
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (operation, owner) = pending.Pop();

            switch (operation)
            {
                case IAnonymousFunctionOperation anonymousFunction:
                    owner = GetThenOwner(anonymousFunction, extensions);
                    break;
                case ILocalFunctionOperation:
                    owner = null;
                    break;
                case IReturnOperation { Kind: OperationKind.Return, ReturnedValue: { } value } when owner is not null:
                    returnedChains.Push(value);
                    while (returnedChains.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        switch (returnedChains.Pop())
                        {
                            case IParenthesizedOperation parenthesized:
                                returnedChains.Push(parenthesized.Operand);
                                break;
                            // Target-typed branch wrappers leave each arm's conversion intact.
                            case IConversionOperation conversion when conversion.GetConversion() is
                                { IsIdentity: true } or { IsConditionalExpression: true } or { IsSwitchExpression: true }:
                                returnedChains.Push(conversion.Operand);
                                break;
                            case IConditionalOperation { WhenFalse: { } whenFalse } conditional:
                                returnedChains.Push(whenFalse);
                                returnedChains.Push(conditional.WhenTrue);
                                break;
                            case ISwitchExpressionOperation switchExpression:
                                foreach (var arm in switchExpression.Arms)
                                {
                                    returnedChains.Push(arm.Value);
                                }
                                break;
                            case IInvocationOperation invocation:
                                var name = GetExtensionName(invocation, extensions);
                                if (name == "Then")
                                {
                                    parents.Add(owner);
                                    children.Add(invocation);
                                }

                                if (name is "Then" or "OnOk" or "OnErr"
                                    && (invocation.Instance ?? GetArgument(invocation, "result")) is { } receiver)
                                {
                                    returnedChains.Push(receiver);
                                }
                                break;
                        }
                    }
                    break;
            }

            foreach (var child in operation.ChildOperations)
            {
                pending.Push((child, owner));
            }
        }

        // A Then that is both a parent and a child belongs to its parent's nesting structure.
        foreach (var parent in parents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!children.Contains(parent) && GetThenNameLocation(parent) is { } location)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NestedThen, location));
            }
        }
    }

    private static IInvocationOperation? GetThenOwner(IAnonymousFunctionOperation function, INamedTypeSymbol extensions)
    {
        var parent = function.Parent;
        while (parent is IDelegateCreationOperation or IParenthesizedOperation
            || parent is IConversionOperation conversion && conversion.Conversion.IsIdentity)
        {
            parent = parent.Parent;
        }

        return parent is IArgumentOperation { Parameter.Name: "onOk", Parent: IInvocationOperation invocation }
            && GetExtensionName(invocation, extensions) == "Then"
            ? invocation
            : null;
    }

    private static string? GetExtensionName(IInvocationOperation invocation, INamedTypeSymbol extensions)
    {
        var method = invocation.TargetMethod;
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        return definition.IsExtensionMethod
            && SymbolEqualityComparer.Default.Equals(definition.ContainingType, extensions)
            && !invocation.Syntax.ContainsDiagnostics
            ? definition.Name
            : null;
    }

    private static IOperation? GetArgument(IInvocationOperation invocation, string parameterName)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument.Value;
            }
        }

        return null;
    }

    private static Location? GetThenNameLocation(IInvocationOperation invocation)
    {
        if (invocation.Syntax is not InvocationExpressionSyntax syntax)
        {
            return null;
        }

        return syntax.Expression switch
        {
            SimpleNameSyntax simpleName => simpleName.Identifier.GetLocation(),
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.GetLocation(),
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.GetLocation(),
            _ => null
        };
    }
}
