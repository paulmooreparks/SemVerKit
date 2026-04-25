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
/// Proves that the Overt-authored SemVer parser round-trips the numeric
/// core of SemVer 2.0.0 and surfaces each known failure mode as a
/// distinct <see cref="ParseError"/> variant.
///
/// These tests cover only the major.minor.patch subset; prerelease and
/// build-metadata tests land with the next round of Overt content.
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
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.2.3")]
    [InlineData("10.20.30")]
    public void Display_RoundTripsNumericVersion(string input) {
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

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("a.2.3")]
    [InlineData("01.2.3")]
    public void Describe_ProducesNonEmptyString(string input) {
        var result = Module.parse(input);
        var err = Assert.IsType<ResultErr<Version, ParseError>>(result);
        var text = Module.describe(err.Error);
        Assert.NotEmpty(text);
    }
}
