using Microsoft.CodeAnalysis.CSharp.Syntax;
using RobloxCSharp.Extensions.Linq;
using RobloxCSharp.Transformer;
using RobloxCSharp.Transformer.AST;
using RobloxCSharp.Transformer.AST.Expressions;

namespace Linq.Tests
{
	public class LinqExtensionTests
	{
		private const string PluginRequire =
			"require(game:GetService(\"ReplicatedStorage\"):WaitForChild(\"Plugins\"):WaitForChild(\"Linq\"):WaitForChild(\"Enumerable\"))";

		private static LinqExtension Subject => new();

		private static (TransformerState State, InvocationExpressionSyntax Invocation) Setup(string body)
		{
			string source = $@"
using System.Collections.Generic;
using System.Linq;

public class Test
{{
	void Run()
	{{
		var numbers = new List<int> {{ 1, 2, 3 }};
		{body};
	}}
}}
";
			(TransformerState state, CompilationUnitSyntax root) = TestHarness.Compile(source);
			InvocationExpressionSyntax invocation = FindRelevantInvocation(root);
			return (state, invocation);
		}

		private static InvocationExpressionSyntax FindRelevantInvocation(CompilationUnitSyntax root)
		{
			foreach (SyntaxNode node in root.DescendantNodes())
			{
				if (node is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax)
				{
					return inv;
				}
			}
			throw new InvalidOperationException("No member-access invocation found.");
		}

		private static (LuaMemberAccessExpression Callee, List<LuaNode> Args) AssertLinqCall(LuaNode result, string expectedMethod)
		{
			LuaInvocationExpression invocation = Assert.IsType<LuaInvocationExpression>(result);
			LuaMemberAccessExpression callee = Assert.IsType<LuaMemberAccessExpression>(invocation.Expression);
			Assert.Equal(expectedMethod, callee.MemberName);
			Assert.False(callee.IsMethodCall);
			LuaIdentifier root = Assert.IsType<LuaIdentifier>(callee.Target);
			Assert.Equal(PluginRequire, root.Name);
			return (callee, invocation.Arguments);
		}

		[Fact]
		public void ReducedExtensionForm_PrependsReceiverAsFirstArg()
		{
			(TransformerState state, InvocationExpressionSyntax inv) = Setup("var r = numbers.Where(x => x > 0).ToList()");

			SyntaxNode where = inv;
			while (where is InvocationExpressionSyntax outer
				&& outer.Expression is MemberAccessExpressionSyntax m
				&& m.Name.Identifier.ValueText != "Where")
			{
				where = m.Expression;
			}

			LuaNode result = Subject.TryRewrite(where, state);

			(LuaMemberAccessExpression _, List<LuaNode> args) = AssertLinqCall(result, "Where");
			Assert.Equal(2, args.Count);
			LuaIdentifier receiver = Assert.IsType<LuaIdentifier>(args[0]);
			Assert.Equal("numbers", receiver.Name);
		}

		[Fact]
		public void StaticForm_DoesNotPrependReceiver()
		{
			(TransformerState state, InvocationExpressionSyntax inv) = Setup("var r = System.Linq.Enumerable.Where(numbers, x => x > 0)");

			LuaNode result = Subject.TryRewrite(inv, state);

			(LuaMemberAccessExpression _, List<LuaNode> args) = AssertLinqCall(result, "Where");
			Assert.Equal(2, args.Count);
			LuaIdentifier first = Assert.IsType<LuaIdentifier>(args[0]);
			Assert.Equal("numbers", first.Name);
		}

		[Fact]
		public void StaticFactoryMethod_RoutesThroughPlugin()
		{
			(TransformerState state, InvocationExpressionSyntax inv) = Setup("var r = System.Linq.Enumerable.Range(0, 5).ToList()");

			SyntaxNode range = null;
			foreach (SyntaxNode node in inv.SyntaxTree.GetRoot().DescendantNodes())
			{
				if (node is InvocationExpressionSyntax i
					&& i.Expression is MemberAccessExpressionSyntax ma
					&& ma.Name.Identifier.ValueText == "Range")
				{
					range = i;
					break;
				}
			}
			Assert.NotNull(range);

			LuaNode result = Subject.TryRewrite(range, state);

			(LuaMemberAccessExpression _, List<LuaNode> args) = AssertLinqCall(result, "Range");
			Assert.Equal(2, args.Count);
		}

		[Fact]
		public void UserDefinedEnumerable_PassesThrough()
		{
			string source = @"
namespace MyApp.Collections
{
	public class Enumerable
	{
		public static int Where(int x, int y) => x + y;
	}
}

public class Test
{
	void Run() { var r = MyApp.Collections.Enumerable.Where(1, 2); }
}
";
			(TransformerState state, CompilationUnitSyntax root) = TestHarness.Compile(source);
			InvocationExpressionSyntax inv = FindRelevantInvocation(root);

			LuaNode result = Subject.TryRewrite(inv, state);

			Assert.Null(result);
		}

		[Fact]
		public void NonLinqInstanceMethod_PassesThrough()
		{
			(TransformerState state, InvocationExpressionSyntax inv) = Setup("numbers.Add(4)");

			LuaNode result = Subject.TryRewrite(inv, state);

			Assert.Null(result);
		}

		[Fact]
		public void NonInvocationNode_PassesThrough()
		{
			(TransformerState state, InvocationExpressionSyntax inv) = Setup("var r = numbers.Where(x => x > 0).ToList()");
			SyntaxNode propertyAccess = inv.DescendantNodes().OfType<MemberAccessExpressionSyntax>().First();

			LuaNode result = Subject.TryRewrite(propertyAccess, state);

			Assert.Null(result);
		}

		[Fact]
		public void InvocationWithoutMemberAccess_PassesThrough()
		{
			string source = @"
public class Test
{
	static int Helper() => 1;
	void Run() { var r = Helper(); }
}
";
			(TransformerState state, CompilationUnitSyntax root) = TestHarness.Compile(source);
			InvocationExpressionSyntax inv = root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.First(i => i.Expression is IdentifierNameSyntax);

			LuaNode result = Subject.TryRewrite(inv, state);

			Assert.Null(result);
		}

		[Fact]
		public void ChainedLinqCall_EachLinkRoutesIndependently()
		{
			(TransformerState state, InvocationExpressionSyntax inv) = Setup(
				"var r = numbers.Where(x => x > 0).Select(x => x * 2).ToList()");

			List<InvocationExpressionSyntax> linqLinks = new();
			foreach (SyntaxNode node in inv.SyntaxTree.GetRoot().DescendantNodes())
			{
				if (node is InvocationExpressionSyntax i
					&& i.Expression is MemberAccessExpressionSyntax ma
					&& (ma.Name.Identifier.ValueText is "Where" or "Select" or "ToList"))
				{
					linqLinks.Add(i);
				}
			}
			Assert.Equal(3, linqLinks.Count);

			foreach (InvocationExpressionSyntax link in linqLinks)
			{
				LuaNode result = Subject.TryRewrite(link, state);
				Assert.NotNull(result);
				Assert.IsType<LuaInvocationExpression>(result);
			}
		}

		[Fact]
		public void Name_IsLinq()
		{
			Assert.Equal("Linq", Subject.Name);
		}
	}
}
