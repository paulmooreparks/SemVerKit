using Overt.Runtime;
using Xunit;

using Version = ParksComputing.SemVer.Version;

namespace ParksComputing.SemVer.Tests;

/// <summary>
/// Covers the SemVer 2.0.0 precedence rules and bump operations.
/// Precedence walks major / minor / patch numerically, then prerelease
/// identifier-by-identifier (numeric &lt; alphanumeric, shorter prefix
/// loses), and ignores build metadata entirely. Each test below maps
/// to a clause of section 11 of the spec.
/// </summary>
public class SemVerCompareTests {
    private static Version Parse(string input) {
        var result = Module.parse(input);
        return Assert.IsType<ResultOk<Version, ParseError>>(result).Value;
    }

    // ---------- precedence ordering ----------------------------------

    [Theory]
    [InlineData("1.0.0", "2.0.0")]   // major
    [InlineData("2.0.0", "2.1.0")]   // minor
    [InlineData("2.1.0", "2.1.1")]   // patch
    public void Compare_NumericCorePrecedence(string lower, string higher) {
        var a = Parse(lower);
        var b = Parse(higher);
        Assert.IsType<Ordering_Less>(Module.compare(a, b));
        Assert.IsType<Ordering_Greater>(Module.compare(b, a));
        Assert.IsType<Ordering_Equal>(Module.compare(a, a));
    }

    [Fact]
    public void Compare_PrereleaseLowerThanRelease() {
        // Spec section 11.3: "When major, minor, and patch are equal, a
        // pre-release version has lower precedence than a normal version."
        var pre = Parse("1.0.0-alpha");
        var rel = Parse("1.0.0");
        Assert.IsType<Ordering_Less>(Module.compare(pre, rel));
        Assert.IsType<Ordering_Greater>(Module.compare(rel, pre));
    }

    [Theory]
    // From the spec's worked example (section 11.4):
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-alpha.beta", "1.0.0-beta")]
    [InlineData("1.0.0-beta", "1.0.0-beta.2")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    public void Compare_SpecChain(string lower, string higher) {
        var a = Parse(lower);
        var b = Parse(higher);
        Assert.IsType<Ordering_Less>(Module.compare(a, b));
    }

    [Fact]
    public void Compare_NumericPreLessThanAlphanumericPre() {
        // Numeric identifiers always have lower precedence than non-
        // numeric identifiers (section 11.4.3).
        var num = Parse("1.0.0-1");
        var alpha = Parse("1.0.0-alpha");
        Assert.IsType<Ordering_Less>(Module.compare(num, alpha));
    }

    [Fact]
    public void Compare_PrereleaseLengthMatters() {
        // "A larger set of pre-release fields has higher precedence than
        // a smaller set, if all of the preceding identifiers are equal."
        var shorter = Parse("1.0.0-alpha");
        var longer = Parse("1.0.0-alpha.1");
        Assert.IsType<Ordering_Less>(Module.compare(shorter, longer));
    }

    [Fact]
    public void Compare_NumericPrereleaseSegmentsCompareNumerically() {
        // 11 > 2 numerically, even though "11" < "2" lexicographically.
        // SemVer specifically calls this out for numeric prerelease IDs.
        var two = Parse("1.0.0-beta.2");
        var eleven = Parse("1.0.0-beta.11");
        Assert.IsType<Ordering_Less>(Module.compare(two, eleven));
    }

    [Fact]
    public void Compare_BuildMetadataIgnored() {
        // Section 10: "Build metadata MUST be ignored when determining
        // version precedence." So `1.0.0+a` and `1.0.0+b` compare Equal.
        var a = Parse("1.0.0+alpha");
        var b = Parse("1.0.0+beta");
        Assert.IsType<Ordering_Equal>(Module.compare(a, b));
    }

    [Fact]
    public void Compare_BuildMetadataIgnoredAcrossPrerelease() {
        // Same precedence rule applies when prerelease is present.
        var a = Parse("1.0.0-rc.1+sha.aaa");
        var b = Parse("1.0.0-rc.1+sha.bbb");
        Assert.IsType<Ordering_Equal>(Module.compare(a, b));
    }

    // ---------- equal_to / less_than convenience ---------------------

    [Fact]
    public void EqualTo_TreatsBuildMetadataAsEqual() {
        Assert.True(Module.equal_to(Parse("1.0.0+a"), Parse("1.0.0+b")));
        Assert.False(Module.equal_to(Parse("1.0.0"), Parse("1.0.1")));
    }

    [Fact]
    public void LessThan_StrictlyOrdersPrereleaseBelowRelease() {
        Assert.True(Module.less_than(Parse("1.0.0-rc.1"), Parse("1.0.0")));
        Assert.False(Module.less_than(Parse("1.0.0"), Parse("1.0.0")));
    }

    // ---------- bump operations --------------------------------------

    [Fact]
    public void BumpMajor_IncrementsMajorAndZeroesEverythingElse() {
        var bumped = Module.bump_major(Parse("1.2.3-rc.1+build.7"));
        Assert.Equal(2, bumped.major);
        Assert.Equal(0, bumped.minor);
        Assert.Equal(0, bumped.patch);
        Assert.Empty(bumped.prerelease.Items);
        Assert.Empty(bumped.build.Items);
        Assert.Equal("2.0.0", Module.display(bumped));
    }

    [Fact]
    public void BumpMinor_IncrementsMinorZeroesPatchClearsTags() {
        var bumped = Module.bump_minor(Parse("1.2.3-alpha"));
        Assert.Equal(1, bumped.major);
        Assert.Equal(3, bumped.minor);
        Assert.Equal(0, bumped.patch);
        Assert.Equal("1.3.0", Module.display(bumped));
    }

    [Fact]
    public void BumpPatch_IncrementsPatchClearsTags() {
        var bumped = Module.bump_patch(Parse("1.2.3+build.42"));
        Assert.Equal(1, bumped.major);
        Assert.Equal(2, bumped.minor);
        Assert.Equal(4, bumped.patch);
        Assert.Empty(bumped.build.Items);
        Assert.Equal("1.2.4", Module.display(bumped));
    }

    [Fact]
    public void BumpedVersionIsGreaterThanOriginal() {
        // Sanity: every bump strictly increases precedence (because it
        // also drops any prerelease, which would have been < release).
        var v = Parse("1.2.3-rc.1");
        Assert.True(Module.less_than(v, Module.bump_patch(v)));
        Assert.True(Module.less_than(v, Module.bump_minor(v)));
        Assert.True(Module.less_than(v, Module.bump_major(v)));
    }
}
