using Coordina.Api.Core.Results;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Tests.Unit.Shared;

public sealed class ValidationErrorsTests
{
  [Theory]
  [InlineData(null, "Name is required.")]
  [InlineData("   ", "Name is required.")]
  [InlineData(" a ", "Name must be at least 2 characters.")]
  [InlineData("abcdef", "Name must be 5 characters or fewer.")]
  public void Required_ReportsOneMessagePerRule(string? value, string expected)
  {
    var errors = new ValidationErrors()
      .Required("Name", value, "Name", minLength: 2, maxLength: 5);

    Assert.Equal([expected], errors.ToDictionary()["Name"]);
  }

  [Fact]
  public void Required_WithValidTrimmedValue_HasNoErrors()
  {
    var errors = new ValidationErrors()
      .Required("Name", "  abc  ", "Name", minLength: 2, maxLength: 3);

    Assert.False(errors.HasErrors);
  }

  [Fact]
  public void MaxLength_IgnoresMissingValue()
  {
    var errors = new ValidationErrors()
      .MaxLength("Description", null, "Description", 3);

    Assert.False(errors.HasErrors);
  }

  [Fact]
  public void MaxLength_WithLongValue_ReportsError()
  {
    var errors = new ValidationErrors()
      .MaxLength("Description", "abcd", "Description", 3);

    Assert.Equal(
      ["Description must be 3 characters or fewer."],
      errors.ToDictionary()["Description"]);
  }

  [Fact]
  public void AddIf_OnlyAddsWhenConditionHolds()
  {
    var errors = new ValidationErrors()
      .AddIf(false, "A", "Skipped.")
      .AddIf(true, "B", "Kept.");

    Assert.False(errors.Contains("A"));
    Assert.True(errors.Contains("B"));
  }

  [Fact]
  public void ToError_ProducesValidationError()
  {
    var error = new ValidationErrors()
      .Add("Title", "Title is required.")
      .ToError();

    Assert.Equal(ErrorKind.Validation, error.Kind);
    Assert.Equal(["Title is required."], error.Fields["Title"]);
  }
}
