namespace RobloxCSharp.Extensions.Linq
{
    // Plugin-loaded extension that routes every `System.Linq.Enumerable.*`
    // invocation into the Luau runtime that ships in this same plugin
    // at ReplicatedStorage.Plugins.Linq.Enumerable.
    //
    // The transpiler's PluginExtensionLoader picks this file up from
    // plugins/Linq/extension/ at startup, in-memory-compiles it
    // against the transpiler's loaded assemblies, and instantiates it
    // alongside the built-in Components / Networking extensions.
    //
    // All other IRobloxCSharpExtension lifecycle hooks are no-ops —
    // LINQ doesn't need import contribution, artifact emission, or
    // post-transform passes.
    public class LinqExtension : IRobloxCSharpExtension
    {
        // Inline require — same pattern the transpiler uses elsewhere
        // for "external type whose source location can't be resolved
        // by the Rojo path resolver." Roblox's require cache dedupes
        // the module load, so emitting per-call is performance-neutral
        // after the first reach.
        private const string PluginRequireExpression =
            "require(game:GetService(\"ReplicatedStorage\"):WaitForChild(\"Plugins\"):WaitForChild(\"Linq\"):WaitForChild(\"Enumerable\"))";

        public string Name => "Linq";

        public void OnCompile(Compilation compilation, IReadOnlyList<Plugin> plugins, DiagnosticBag diagnostics)
        {
            // No compilation-wide caching needed — every call we care
            // about is matched purely by symbol containing-type, which
            // SemanticModel resolves on demand.
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

            // Reduced extension form: `seq.Select(fn)` — receiver is
            // member.Expression and slides into argument position 0
            // so the call matches the static-shape that the Luau
            // runtime expects: Enumerable.Select(seq, fn).
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

        // Whitelist via the symbol's containing type — only methods
        // declared on System.Linq.Enumerable match. A user class
        // named "Enumerable" in their own project passes through
        // untouched and gets the default invocation lowering.
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
