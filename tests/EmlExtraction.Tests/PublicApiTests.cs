using System.Reflection;
using System.Runtime.InteropServices;

namespace EmlExtraction.Tests;

/// <summary>
/// Public API must be identical on net8.0 and net10.0 (ADR-019). Each target reads the
/// metadata of both built EmlExtraction.dll files and compares the public surface, so the
/// same test fails on either target if the two drift.
/// </summary>
public sealed class PublicApiTests
{
    private static readonly string[] Targets = ["net8.0", "net10.0"];

    [Fact]
    public void PublicApiIsIdenticalOnNet8AndNet10()
    {
        var outputDir = new DirectoryInfo(AppContext.BaseDirectory);
        var thisTarget = outputDir.Name;
        Assert.Contains(thisTarget, Targets);
        var otherTarget = Targets.Single(t => t != thisTarget);
        var otherDll = Path.Combine(outputDir.Parent!.FullName, otherTarget, "EmlExtraction.dll");
        Assert.True(File.Exists(otherDll), $"Build both targets first; missing {otherDll}");

        var mine = Describe(Path.Combine(outputDir.FullName, "EmlExtraction.dll"));
        var other = Describe(otherDll);

        Assert.NotEmpty(mine);
        Assert.Equal(mine, other);
    }

    private static List<string> Describe(string assemblyPath)
    {
        // Resolve against the running runtime plus the assembly's own dependencies.
        var paths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Concat(Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll"));
        using var context = new MetadataLoadContext(new PathAssemblyResolver(paths));
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var interfaces = string.Join(", ", type.GetInterfaces().Select(i => i.FullName).OrderBy(n => n, StringComparer.Ordinal));
            lines.Add($"type {type.Attributes} {type.FullName} : {type.BaseType?.FullName} [{interfaces}]");
            lines.AddRange(type.GetMembers(Flags)
                .Where(m => m is not Type && IsVisible(m))
                .Select(m => $"  {m.MemberType} {Modifiers(m)} {m}")
                .OrderBy(s => s, StringComparer.Ordinal));
        }
        return lines;
    }

    // Public, protected and protected-internal members are visible to consumers.
    private static bool IsVisible(MemberInfo member) => member switch
    {
        MethodBase m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
        FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
        PropertyInfo p => p.GetAccessors(nonPublic: true).Any(IsVisible),
        EventInfo e => e.AddMethod is { } add && IsVisible(add),
        _ => false,
    };

    private static string Modifiers(MemberInfo member) => member switch
    {
        MethodBase m => m.Attributes.ToString(),
        FieldInfo f => f.IsLiteral ? $"{f.Attributes} = {f.GetRawConstantValue()}" : f.Attributes.ToString(),
        _ => string.Empty,
    };
}
