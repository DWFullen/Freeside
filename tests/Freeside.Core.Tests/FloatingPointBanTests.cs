using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Freeside.Core.Tests;

/// <summary>
/// No binary floating point in the money-path projects (AGENTS.md §2, invariant 5). The banned-API
/// analyzer (src/BannedSymbols.txt) catches banned members and APIs that return doubles, but not
/// declarations, casts or literals, so this test parses the source and catches those.
/// </summary>
public sealed class FloatingPointBanTests
{
    private static readonly string[] _moneyPathProjects = ["src/Freeside.Core", "src/Freeside.Infrastructure"];

    [Fact]
    public void Money_path_projects_contain_no_floating_point_types_or_literals()
    {
        var root = RepoRoot();
        var violations = _moneyPathProjects
            .Select(project => Path.Combine(root, project))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => FloatingPointScanner.Scan(File.ReadAllText(path))
                .Select(v => $"{Path.GetRelativePath(root, path)}:{v}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("class C { double d; }")]
    [InlineData("class C { float f; }")]
    [InlineData("class C { System.Double d; }")]
    [InlineData("class C { System.Single s; }")]
    [InlineData("class C { System.Half h; }")]
    [InlineData("class C { Half h; }")]
    [InlineData("class C { object o = 1.5; }")]
    [InlineData("class C { object o = 2f; }")]
    [InlineData("class C { object o = 3d; }")]
    [InlineData("class C { object o = 1e3; }")]
    [InlineData("class C { object o = 10 / 3.0; }")]
    [InlineData("class C { object o = (double)10m; }")]
    [InlineData("class C { object o = typeof(float); }")]
    [InlineData("class C { object o = MathF.PI; }")]
    public void The_scanner_finds_floating_point(string source) =>
        Assert.NotEmpty(FloatingPointScanner.Scan(source));

    [Theory]
    [InlineData("class C { decimal m = 1.5m; }")]
    [InlineData("class C { long l = 1_000; int i = 0x1F; }")]
    [InlineData("class C { // a double-check, 1.5 and float in a comment\n}")]
    [InlineData("class C { string s = \"double 1.5 float\"; }")]
    [InlineData("class C { object o = new[] { 1 }.Single(); }")]
    [InlineData("class C { void Double() { } void M() => this.Double(); }")]
    public void The_scanner_ignores_what_is_not_floating_point(string source) =>
        Assert.Empty(FloatingPointScanner.Scan(source));

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Freeside.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Freeside.slnx not found above " + AppContext.BaseDirectory);
    }
}

internal static class FloatingPointScanner
{
    private static readonly HashSet<string> _bannedTypeNames = ["Double", "Single", "Half", "MathF"];

    /// <summary>Returns "line: text" for each floating-point type name, keyword or literal.</summary>
    public static IEnumerable<string> Scan(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var root = tree.GetRoot();

        var keywords = root.DescendantNodes().OfType<PredefinedTypeSyntax>()
            .Where(node => node.Keyword.IsKind(SyntaxKind.DoubleKeyword) || node.Keyword.IsKind(SyntaxKind.FloatKeyword))
            .Select(node => (SyntaxNodeOrToken)node);

        var names = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(node => _bannedTypeNames.Contains(node.Identifier.ValueText) && !IsMemberOfNonSystemExpression(node))
            .Select(node => (SyntaxNodeOrToken)node);

        var literals = root.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.NumericLiteralToken) && token.Value is double or float)
            .Select(token => (SyntaxNodeOrToken)token);

        return keywords.Concat(names).Concat(literals)
            .OrderBy(item => item.SpanStart)
            .Select(item => $"{item.GetLocation()!.GetLineSpan().StartLinePosition.Line + 1}: {item}");
    }

    // "items.Single()" or "this.Double()" is a member call, not the System type; "System.Double" is.
    private static bool IsMemberOfNonSystemExpression(IdentifierNameSyntax node) =>
        node.Parent is MemberAccessExpressionSyntax access
            && access.Name == node
            && access.Expression.ToString() is not ("System" or "global::System");
}
