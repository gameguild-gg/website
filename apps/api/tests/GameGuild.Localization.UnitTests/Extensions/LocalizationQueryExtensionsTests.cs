using FluentAssertions;
using GameGuild.Localization;
using Xunit;

namespace GameGuild.Tests.Localization.Unit.Extensions;

/// <summary>
/// Tests for LocalizationQueryExtensions
/// </summary>
public class LocalizationQueryExtensionsTests
{
    // Test implementation of ILocalizable
    private class TestLocalizableEntity : ILocalizable
    {
        public ICollection<ResourceLocalization> Localizations { get; set; }
            = new List<ResourceLocalization>();

        public ResourceLocalization AddLocalization(
            string fieldName,
            string content,
            Language language,
            LocalizationStatus status = LocalizationStatus.Draft)
        {
            var localization = new ResourceLocalization
            {
                FieldName = fieldName,
                Content = content,
                Language = language,
                LanguageId = language.Id,
                Status = status
            };
            Localizations.Add(localization);
            return localization;
        }
    }

    private static Language CreateLanguage(string code = "en-US")
    {
        return new Language
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code == "en-US" ? "English" : "Spanish",
            IsActive = true
        };
    }

    [Fact]
    public void IncludeLocalizations_ReturnsQueryable()
    {
        var query = new List<TestLocalizableEntity> { new() }.AsQueryable();

        var result = query.IncludeLocalizations();

        result.ToList().Should().HaveCount(1);
    }

    [Fact]
    public void IncludeLocalizationsWithLanguage_ReturnsQueryable()
    {
        var query = new List<TestLocalizableEntity> { new() }.AsQueryable();

        var result = query.IncludeLocalizationsWithLanguage();

        result.ToList().Should().HaveCount(1);
    }

    [Fact]
    public void GetLocalizedField_ReturnsLocalizedContent()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        entity.AddLocalization("Title", "Hello World", english);

        // Act
        var result = LocalizationQueryExtensions.GetLocalizedField(entity, "Title", english.Id, "Fallback");

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void GetLocalizedField_ReturnsFallback_WhenNotFound()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        var spanish = CreateLanguage("es-ES");
        entity.AddLocalization("Title", "Hello World", english);

        // Act
        var result = LocalizationQueryExtensions.GetLocalizedField(entity, "Title", spanish.Id, "Fallback");

        // Assert
        result.Should().Be("Fallback");
    }

    [Fact]
    public void GetLocalizedField_ReturnsFallback_WhenFieldNotFound()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        entity.AddLocalization("Title", "Hello World", english);

        // Act
        var result = LocalizationQueryExtensions.GetLocalizedField(entity, "Description", english.Id, "No description");

        // Assert
        result.Should().Be("No description");
    }

    [Fact]
    public void GetLocalizedFieldByCode_ReturnsLocalizedContent()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        entity.AddLocalization("Title", "Hello World", english);

        // Act
        var result = LocalizationQueryExtensions.GetLocalizedFieldByCode(entity, "Title", "en-US", "Fallback");

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void GetLocalizedFieldByCode_ReturnsFallback_WhenLanguageNotFound()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        entity.AddLocalization("Title", "Hello World", english);

        // Act
        var result = LocalizationQueryExtensions.GetLocalizedFieldByCode(entity, "Title", "fr-FR", "Fallback");

        // Assert
        result.Should().Be("Fallback");
    }

    [Fact]
    public void HasLocalizationFor_ReturnsTrue_WhenExists()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        entity.AddLocalization("Title", "Hello World", english);

        // Act
        var result = LocalizationQueryExtensions.HasLocalizationFor(entity, "Title", english.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasLocalizationFor_ReturnsFalse_WhenNotExists()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");

        // Act
        var result = LocalizationQueryExtensions.HasLocalizationFor(entity, "Title", english.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAvailableLanguages_ReturnsDistinctLanguages()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");
        var spanish = CreateLanguage("es-ES");

        entity.AddLocalization("Title", "Hello", english);
        entity.AddLocalization("Description", "World", english);
        entity.AddLocalization("Title", "Hola", spanish);

        // Act
        var result = LocalizationQueryExtensions.GetAvailableLanguages(entity).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(l => l.Code == "en-US");
        result.Should().Contain(l => l.Code == "es-ES");
    }

    [Fact]
    public void GetAllLocalizedFields_ReturnsDictionary()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");

        entity.AddLocalization("Title", "Hello", english);
        entity.AddLocalization("Description", "World", english);

        // Act
        var result = LocalizationQueryExtensions.GetAllLocalizedFields(entity, english.Id);

        // Assert
        result.Should().HaveCount(2);
        result["Title"].Should().Be("Hello");
        result["Description"].Should().Be("World");
    }

    [Fact]
    public void GetAllLocalizedFields_ReturnsEmpty_WhenNoLocalizations()
    {
        // Arrange
        var entity = new TestLocalizableEntity();
        var english = CreateLanguage("en-US");

        // Act
        var result = LocalizationQueryExtensions.GetAllLocalizedFields(entity, english.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetLocalizedField_ThrowsOnNullEntity()
    {
        // Arrange
        TestLocalizableEntity? entity = null;

        // Act & Assert
        var act = () => LocalizationQueryExtensions.GetLocalizedField(entity!, "Title", Guid.NewGuid(), "Fallback");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetLocalizedField_ThrowsOnNullFieldName()
    {
        // Arrange
        var entity = new TestLocalizableEntity();

        // Act & Assert
        var act = () => LocalizationQueryExtensions.GetLocalizedField(entity, null!, Guid.NewGuid(), "Fallback");
        act.Should().Throw<ArgumentNullException>().WithParameterName("fieldName");
    }
}
