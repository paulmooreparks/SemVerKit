using Overt.Runtime;
using Xunit;

// `Version` collides with `System.Version` under ImplicitUsings;
// alias the one from our own namespace to disambiguate. The other
// types (Module, ParseError, ParseError_*) are visible without a
// using directive because the test namespace is a child of the
// library namespace.
using Version = ParksComputing.SemVer.Version;

namespace ParksComputing.SemVer.Tests;

/// <summary>
/// Proves that the Overt-authored SemVer parser round-trips the full
/// SemVer 2.0.0 grammar (numeric core, prerelease, and build metadata)
/// and surfaces each known failure mode as a distinct
/// <see cref="ParseError"/> variant.
/// </summary>
public class SemVerParseTests {
    [Theory]
    [InlineData("0.0.0", 0, 0, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("10.20.30", 10, 20, 30)]
    [InlineData("0.1.0", 0, 1, 0)]
    public void Parse_ValidNumericVersion_ReturnsVersion(string input, int major, int minor, int patch) {
        var result = Module.parse(input);
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Equal(major, ok.Value.major);
        Assert.Equal(minor, ok.Value.minor);
        Assert.Equal(patch, ok.Value.patch);
        Assert.Empty(ok.Value.prerelease.Items);
        Assert.Empty(ok.Value.build.Items);
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.2.3")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0-0.3.7")]
    [InlineData("1.0.0-x.7.z.92")]
    [InlineData("1.0.0-x-y-z.--")]
    [InlineData("1.0.0+20130313144700")]
    [InlineData("1.0.0-beta+exp.sha.5114f85")]
    [InlineData("1.0.0+21AF26D3----117B344092BD")]
    public void Display_RoundTripsFullGrammar(string input) {
        var parsed = Module.parse(input);
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(parsed);
        Assert.Equal(input, Module.display(ok.Value));
    }

    [Fact]
    public void Parse_Empty_ReturnsEmptyError() {
        var result = Module.parse("");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_Empty>(err.Error);
    }

    [Theory]
    [InlineData("-alpha")]
    [InlineData("+build")]
    [InlineData("-rc.1+build.7")]
    public void Parse_NoNumericCore_ReturnsEmpty(string input) {
        // Stripping prerelease/build leaves nothing to parse as a
        // major.minor.patch core, so this is reported as Empty rather
        // than MissingMinor (the latter implies a 1-segment numeric
        // core, which we don't actually have).
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_Empty>(err.Error);
    }

    [Fact]
    public void Parse_SingleSegment_ReturnsMissingMinor() {
        var result = Module.parse("1");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_MissingMinor>(err.Error);
    }

    [Fact]
    public void Parse_TwoSegments_ReturnsMissingPatch() {
        var result = Module.parse("1.2");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_MissingPatch>(err.Error);
    }

    [Fact]
    public void Parse_FourSegments_ReturnsTooManySegments() {
        var result = Module.parse("1.2.3.4");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var variant = Assert.IsType<ParseError_TooManySegments>(err.Error);
        Assert.Equal(4, variant.got);
    }

    [Theory]
    [InlineData("a.2.3", "major", "a")]
    [InlineData("1.b.3", "minor", "b")]
    [InlineData("1.2.c", "patch", "c")]
    [InlineData("1.2.3x", "patch", "3x")]
    public void Parse_NonNumericSegment_ReturnsNonNumericSegment(string input, string field, string got) {
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var variant = Assert.IsType<ParseError_NonNumericSegment>(err.Error);
        Assert.Equal(field, variant.field);
        Assert.Equal(got, variant.got);
    }

    [Theory]
    [InlineData("01.2.3", "major", "01")]
    [InlineData("1.02.3", "minor", "02")]
    [InlineData("1.2.03", "patch", "03")]
    public void Parse_LeadingZero_ReturnsLeadingZeroSegment(string input, string field, string got) {
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var variant = Assert.IsType<ParseError_LeadingZeroSegment>(err.Error);
        Assert.Equal(field, variant.field);
        Assert.Equal(got, variant.got);
    }

    [Theory]
    [InlineData("..3", "major")]
    [InlineData("1..3", "minor")]
    [InlineData("1.2.", "patch")]
    public void Parse_EmptySegment_ReturnsEmptySegment(string input, string field) {
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var variant = Assert.IsType<ParseError_EmptySegment>(err.Error);
        Assert.Equal(field, variant.field);
    }

    // ---------- prerelease parsing -----------------------------------

    [Fact]
    public void Parse_AlphanumericPrerelease_ReturnsAlphanumericVariant() {
        var result = Module.parse("1.0.0-alpha");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Single(ok.Value.prerelease.Items);
        var id = Assert.IsType<PrereleaseId_Alphanumeric>(ok.Value.prerelease.Items[0]);
        Assert.Equal("alpha", id.text);
    }

    [Fact]
    public void Parse_NumericPrerelease_ReturnsNumericVariant() {
        var result = Module.parse("1.0.0-7");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Single(ok.Value.prerelease.Items);
        var id = Assert.IsType<PrereleaseId_Numeric>(ok.Value.prerelease.Items[0]);
        Assert.Equal(7, id.value);
    }

    [Fact]
    public void Parse_MixedPrerelease_PreservesOrderAndKinds() {
        // `alpha.1.beta-rc` exercises all three identifier shapes:
        // alphanumeric, numeric, and alphanumeric with embedded dash.
        var result = Module.parse("1.0.0-alpha.1.beta-rc");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        var ids = ok.Value.prerelease.Items;
        Assert.Equal(3, ids.Length);
        Assert.Equal("alpha", Assert.IsType<PrereleaseId_Alphanumeric>(ids[0]).text);
        Assert.Equal(1,       Assert.IsType<PrereleaseId_Numeric>(ids[1]).value);
        Assert.Equal("beta-rc", Assert.IsType<PrereleaseId_Alphanumeric>(ids[2]).text);
    }

    [Fact]
    public void Parse_DanglingDash_ReturnsEmptyPrerelease() {
        var result = Module.parse("1.0.0-");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_EmptyPrerelease>(err.Error);
    }

    [Fact]
    public void Parse_EmptyPrereleaseSegment_ReturnsEmptyPrereleaseSegment() {
        var result = Module.parse("1.0.0-alpha..beta");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_EmptyPrereleaseSegment>(err.Error);
    }

    [Fact]
    public void Parse_InvalidPrereleaseChar_ReturnsInvalidPrereleaseChar() {
        var result = Module.parse("1.0.0-alpha!");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var variant = Assert.IsType<ParseError_InvalidPrereleaseChar>(err.Error);
        Assert.Equal("alpha!", variant.segment);
    }

    [Theory]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0-alpha.001")]
    public void Parse_LeadingZeroNumericPrereleaseSegment_ReturnsLeadingZeroPrereleaseSegment(string input) {
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_LeadingZeroPrereleaseSegment>(err.Error);
    }

    [Fact]
    public void Parse_AlphanumericLeadingZero_IsAccepted() {
        // 0a1 is alphanumeric (contains a letter), so the no-leading-
        // zero rule does not apply — leading zeros only matter for
        // pure-digit identifiers.
        var result = Module.parse("1.0.0-0a1");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.IsType<PrereleaseId_Alphanumeric>(ok.Value.prerelease.Items[0]);
    }

    // ---------- build-metadata parsing -------------------------------

    [Fact]
    public void Parse_BuildMetadata_PreservesSegmentsAsStrings() {
        var result = Module.parse("1.0.0+build.20260424");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Equal(2, ok.Value.build.Items.Length);
        Assert.Equal("build", ok.Value.build.Items[0]);
        Assert.Equal("20260424", ok.Value.build.Items[1]);
    }

    [Fact]
    public void Parse_BuildMetadataLeadingZero_IsAccepted() {
        // Build metadata identifiers are opaque — leading zeros are
        // explicitly allowed by the spec, unlike numeric prerelease IDs.
        var result = Module.parse("1.0.0+0001");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Equal("0001", ok.Value.build.Items[0]);
    }

    [Fact]
    public void Parse_BuildMetadataDashInsideSegment_IsAccepted() {
        // Build identifiers may contain `-`; the parser splits at the
        // first `+` only, so the dash inside the build chunk doesn't
        // get re-parsed as a prerelease marker.
        var result = Module.parse("1.0.0+abc-def");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Single(ok.Value.build.Items);
        Assert.Equal("abc-def", ok.Value.build.Items[0]);
        Assert.Empty(ok.Value.prerelease.Items);
    }

    [Fact]
    public void Parse_DanglingPlus_ReturnsEmptyBuild() {
        var result = Module.parse("1.0.0+");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_EmptyBuild>(err.Error);
    }

    [Fact]
    public void Parse_EmptyBuildSegment_ReturnsEmptyBuildSegment() {
        var result = Module.parse("1.0.0+abc..def");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        Assert.IsType<ParseError_EmptyBuildSegment>(err.Error);
    }

    [Fact]
    public void Parse_InvalidBuildChar_ReturnsInvalidBuildChar() {
        var result = Module.parse("1.0.0+abc!");
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var variant = Assert.IsType<ParseError_InvalidBuildChar>(err.Error);
        Assert.Equal("abc!", variant.segment);
    }

    // ---------- combined prerelease + build --------------------------

    [Fact]
    public void Parse_PrereleaseAndBuild_BothPopulated() {
        var result = Module.parse("1.0.0-rc.1+build.20260424");
        var ok = Assert.IsType<ResultOk<Version, ParseError>>(result);
        Assert.Equal(1, ok.Value.major);
        Assert.Equal(0, ok.Value.minor);
        Assert.Equal(0, ok.Value.patch);
        Assert.Equal(2, ok.Value.prerelease.Items.Length);
        Assert.Equal("rc", Assert.IsType<PrereleaseId_Alphanumeric>(ok.Value.prerelease.Items[0]).text);
        Assert.Equal(1, Assert.IsType<PrereleaseId_Numeric>(ok.Value.prerelease.Items[1]).value);
        Assert.Equal(2, ok.Value.build.Items.Length);
        Assert.Equal("build", ok.Value.build.Items[0]);
        Assert.Equal("20260424", ok.Value.build.Items[1]);
    }

    // ---------- describe ---------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("a.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-alpha!")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+abc!")]
    public void Describe_ProducesNonEmptyString(string input) {
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var text = Module.describe(err.Error);
        Assert.NotEmpty(text);
    }
}
