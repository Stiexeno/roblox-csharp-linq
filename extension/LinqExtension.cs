namespace RobloxCSharp.Extensions.Linq
{
    /// <summary>
    /// Transpiler hook that rewrites every <c>System.Linq.Enumerable</c>
    /// invocation into a call against the runtime module mounted at
    /// <c>ReplicatedStorage.Plugins.Linq.Enumerable</c>.
    /// </summary>
    public class LinqExtension : IRobloxCSharpExtension
    {
        private const string PluginRequireExpression =
            "require(game:GetService(\"ReplicatedStorage\"):WaitForChild(\"Plugins\"):WaitForChild(\"Linq\"):WaitForChild(\"Enumerable\"))";

        public string Name => "Linq";

        public void OnCompile(Compilation compilation, IReadOnlyList<Plugin> plugins, DiagnosticBag diagnostics)
        {

        }

        public LuaNode TryRewrite(SyntaxNode syntax, TransformerState state)
        {
            if (syntax is not InvocationExpressionSyntax invocation) return null;
            if (invocation.Expression is not MemberAccessExpressionSyntax member) return null;
            if (state.SemanticModel.GetSymbolInfo(member).Symbol is not IMethodSymbol method) return null;
            if (!IsLinqEnumerableMethod(method)) return null;

            LuaExpression classRef = LuaFactory.Identifier(PluginRequireExpression);
            LuaExpression callee = LuaFactory.MemberAccess(classRef, method.Name, isMethodCall: false);
            LuaInvocationExpression call = LuaFactory.Invocation(callee);

            if (method.MethodKind == MethodKind.ReducedExtension)
            {
                LuaExpression receiver = state.Transform(member.Expression) as LuaExpression;
                call.Arguments.Add(receiver);
            }

            foreach (ArgumentSyntax argumentSyntax in invocation.ArgumentList.Arguments)
            {
                LuaExpression argument = state.Transform(argumentSyntax.Expression) as LuaExpression;
                call.Arguments.Add(argument);
            }

            return call;
        }

        public IEnumerable<INamedTypeSymbol> ContributeImports(CompilationUnitSyntax syntax, TransformerState state)
            => Array.Empty<INamedTypeSymbol>();

        public IEnumerable<INamedTypeSymbol> SuppressImports(CompilationUnitSyntax syntax, TransformerState state)
            => Array.Empty<INamedTypeSymbol>();

        public void OnUnitTransformed(LuaCompilationUnit unit, CompilationUnitSyntax syntax, TransformerState state)
        {
        }

        public void EmitArtifacts(string outDir, IReadOnlyList<Plugin> plugins, RojoResolver resolver, DiagnosticBag diagnostics)
        {
        }

        private static bool IsLinqEnumerableMethod(IMethodSymbol method)
        {
            INamedTypeSymbol containing = method.ContainingType;
            if (containing is null) return false;
            if (containing.Name != "Enumerable") return false;

            INamespaceSymbol ns = containing.ContainingNamespace;
            return ns is { Name: "Linq" }
                && ns.ContainingNamespace is { Name: "System" };
        }
    }
}
