using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RobloxCSharp;
using RobloxCSharp.Transformer;

namespace Linq.Tests
{
	internal static class TestHarness
	{
		public static (TransformerState State, CompilationUnitSyntax Root) Compile(string userSource)
		{
			SyntaxTree userTree = CSharpSyntaxTree.ParseText(userSource);
			CSharpCompilation compilation = CompilationFactory.Create("Anonymous", userTree);
			CSharpCompilationContext context = new(userTree, compilation);
			TransformerState state = new(context);
			return (state, (CompilationUnitSyntax)userTree.GetRoot());
		}

		public static InvocationExpressionSyntax FirstInvocation(CompilationUnitSyntax root)
		{
			foreach (SyntaxNode node in root.DescendantNodes())
			{
				if (node is InvocationExpressionSyntax inv) return inv;
			}
			throw new InvalidOperationException("No invocation found in source.");
		}
	}
}
