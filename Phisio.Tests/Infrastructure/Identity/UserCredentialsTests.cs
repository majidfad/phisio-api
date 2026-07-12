using FluentAssertions;
using Phisio.Infrastructure.Identity;

namespace Phisio.Tests.Infrastructure.Identity;

public class UserCredentialsTests
{
    [Theory]
    [InlineData("+989129998877", "+989129998877")]
    [InlineData("989129998877", "+989129998877")]
    [InlineData("+98 912 999 8877", "+989129998877")]
    [InlineData("  +989129998877  ", "+989129998877")]
    [InlineData("(989) 129-998877", "+989129998877")]
    public void NormalizePhone_ReturnsCanonicalE164StyleValue(string input, string expected)
    {
        UserCredentials.NormalizePhone(input).Should().Be(expected);
    }

    [Fact]
    public void GetPhoneLookupValues_IncludesCanonicalAndDigitsOnlyForms()
    {
        var values = UserCredentials.GetPhoneLookupValues("989129998877");

        values.Should().BeEquivalentTo(["+989129998877", "989129998877"]);
    }
}
