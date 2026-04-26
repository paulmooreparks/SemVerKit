using System.Diagnostics;
using Xunit;

namespace ParksComputing.SemVer.Cli.Tests;

/// <summary>
/// End-to-end tests for `ovsemver` that invoke the built executable as
/// a subprocess. The CLI is authored in Overt (Program.ov) and emits a
/// synthesized C# entry point at build time, so there's no internal
/// `Cli` class to poke at directly — every behaviour the shell sees has
/// to flow through the same dotnet-exec path the published global tool
/// uses. That's the contract these tests lock in.
///
/// The CLI project is referenced via &lt;ProjectReference&gt; so it builds
/// before the tests run; we locate the built `ovsemver.dll` by walking
/// up from the test bin directory to the repo root and back down to the
/// CLI's output directory. Configuration (Debug/Release) is picked up
/// from the path the test runner is using.
/// </summary>
public class OvsemverCliTests {
    [Fact]
    public void Parse_Valid_EchoesAndExitsZero() {
        var (exit, stdout, stderr) = Run("parse", "1.2.3-rc.1+build.7");
        Assert.Equal(0, exit);
        Assert.Equal("1.2.3-rc.1+build.7", stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public void Parse_Invalid_ReportsAndExitsOne() {
        var (exit, stdout, stderr) = Run("parse", "01.2.3");
        Assert.Equal(1, exit);
        Assert.Equal("", stdout);
        Assert.Contains("leading zero", stderr);
    }

    [Fact]
    public void Parse_MissingArg_ExitsTwo() {
        var (exit, _, stderr) = Run("parse");
        Assert.Equal(2, exit);
        Assert.Contains("ovsemver parse", stderr);
    }

    // ---------- compare ----------------------------------------------

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0", "<", 1)]
    [InlineData("1.0.0", "1.0.0", "=", 0)]
    [InlineData("2.0.0", "1.99.99", ">", 2)]
    [InlineData("1.0.0+a", "1.0.0+b", "=", 0)]   // build metadata ignored
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11", "<", 1)]  // numeric prerelease compare
    public void Compare_PrintsGlyphAndCmpStyleExitCode(
        string a, string b, string glyph, int expectedExit) {
        var (exit, stdout, stderr) = Run("compare", a, b);
        Assert.Equal(expectedExit, exit);
        Assert.Equal(glyph, stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public void Compare_LeftInvalid_ReportsAndExitsOne() {
        var (exit, stdout, stderr) = Run("compare", "garbage", "1.0.0");
        Assert.Equal(1, exit);
        Assert.Equal("", stdout);
        Assert.Contains("left", stderr);
    }

    [Fact]
    public void Compare_RightInvalid_ReportsAndExitsOne() {
        var (exit, _, stderr) = Run("compare", "1.0.0", "garbage");
        Assert.Equal(1, exit);
        Assert.Contains("right", stderr);
    }

    // ---------- bump -------------------------------------------------

    [Theory]
    [InlineData("major", "1.2.3-rc.1+build.7", "2.0.0")]
    [InlineData("minor", "1.2.3-rc.1", "1.3.0")]
    [InlineData("patch", "1.2.3+build.42", "1.2.4")]
    public void Bump_PrintsBumpedVersion(string component, string input, string expected) {
        var (exit, stdout, stderr) = Run("bump", component, input);
        Assert.Equal(0, exit);
        Assert.Equal(expected, stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public void Bump_UnknownComponent_ExitsTwo() {
        var (exit, _, stderr) = Run("bump", "wibble", "1.0.0");
        Assert.Equal(2, exit);
        Assert.Contains("expected major | minor | patch", stderr);
    }

    [Fact]
    public void Bump_InvalidVersion_ExitsOne() {
        var (exit, _, stderr) = Run("bump", "patch", "01.2.3");
        Assert.Equal(1, exit);
        Assert.Contains("leading zero", stderr);
    }

    // ---------- top-level dispatch -----------------------------------

    [Fact]
    public void NoArgs_PrintsUsageAndExitsTwo() {
        var (exit, _, stderr) = Run();
        Assert.Equal(2, exit);
        Assert.Contains("usage", stderr);
    }

    [Fact]
    public void UnknownCommand_PrintsUsageAndExitsTwo() {
        var (exit, _, stderr) = Run("does-not-exist");
        Assert.Equal(2, exit);
        Assert.Contains("usage", stderr);
    }

    [Fact]
    public void Help_PrintsUsageOnStdoutAndExitsZero() {
        var (exit, stdout, stderr) = Run("--help");
        Assert.Equal(0, exit);
        Assert.Contains("ovsemver", stdout);
        Assert.Contains("parse", stdout);
        Assert.Contains("compare", stdout);
        Assert.Contains("bump", stdout);
        Assert.Equal("", stderr);
    }

    // ----------------------------------------------------------------
    // Subprocess plumbing.
    // ----------------------------------------------------------------

    private static readonly Lazy<string> OvsemverDll = new(LocateOvsemverDll);

    private static (int exit, string stdout, string stderr) Run(params string[] args) {
        var psi = new ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(OvsemverDll.Value);
        foreach (var a in args) {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');
        var stderr = p.StandardError.ReadToEnd().TrimEnd('\r', '\n');
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Walk up from the test's runtime bin directory to the repo root
    /// (identified by SemVerKit.sln), then back down to the CLI's
    /// output directory in the same configuration. The configuration
    /// folder name (Debug / Release) is reused from the test's own
    /// path, so a `dotnet test -c Release` run finds the matching
    /// `Release/ovsemver.dll`.
    /// </summary>
    private static string LocateOvsemverDll() {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        // Expect: <root>/tests/ParksComputing.SemVer.Cli.Tests/bin/<Cfg>/<TFM>/
        var tfm = here.Name;
        var configuration = here.Parent?.Name
            ?? throw new InvalidOperationException(
                "test bin path does not contain a configuration directory: " + here.FullName);

        var root = here;
        while (root is not null && root.GetFiles("SemVerKit.sln").Length == 0) {
            root = root.Parent;
        }
        if (root is null) {
            throw new InvalidOperationException(
                "could not find SemVerKit.sln walking up from " + AppContext.BaseDirectory);
        }

        var dll = Path.Combine(
            root.FullName, "src", "ParksComputing.SemVer.Cli",
            "bin", configuration, tfm, "ovsemver.dll");

        if (!File.Exists(dll)) {
            throw new InvalidOperationException(
                "ovsemver.dll not found at expected path: " + dll
                + " — was the CLI project built? (project reference normally ensures this).");
        }
        return dll;
    }
}
