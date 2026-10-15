using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GameGuild;
using GameGuild.Content.Pages;
using Xunit;

namespace GameGuild.Content.Pages.UnitTests;

#region Entity Tests

public class PageTests
{
    [Fact]
    public void Page_DefaultValues_ShouldBeCorrect()
    {
        var page = new Page();

        page.Slug.Should().Be(string.Empty);
        page.Title.Should().Be(string.Empty);
        page.Description.Should().BeNull();
        page.PageType.Should().Be(PageType.Landing);
        page.Status.Should().Be(PageStatus.Draft);
        page.Locale.Should().BeNull();
        page.SortOrder.Should().Be(0);
        page.ParentPageId.Should().BeNull();
    }

    [Fact]
    public void Page_ShouldSetProperties()
    {
        var parentId = Guid.NewGuid();
        var page = new Page
        {
            Slug = "about-us",
            Title = "About Us",
            Description = "Our story",
            PageType = PageType.Legal,
            Status = PageStatus.Published,
            Locale = "en-US",
            MetaTitle = "About - GameGuild",
            MetaDescription = "Learn about GameGuild",
            MetaKeywords = "gameguild, about",
            CanonicalUrl = "https://gameguild.com/about",
            RobotsDirective = "index, follow",
            OgTitle = "About GameGuild",
            OgDescription = "Our story",
            OgImageUrl = "https://img.com/og.png",
            OgType = "website",
            TwitterCard = "summary_large_image",
            TwitterSite = "@gameguild",
            Body = "<h1>About</h1>",
            ParentPageId = parentId,
            SortOrder = 5
        };

        page.Slug.Should().Be("about-us");
        page.Title.Should().Be("About Us");
        page.PageType.Should().Be(PageType.Legal);
        page.Status.Should().Be(PageStatus.Published);
        page.OgTitle.Should().Be("About GameGuild");
        page.TwitterCard.Should().Be("summary_large_image");
        page.ParentPageId.Should().Be(parentId);
        page.SortOrder.Should().Be(5);
    }

    [Fact]
    public void Page_Collections_ShouldBeInitialized()
    {
        var page = new Page();

        page.ChildPages.Should().NotBeNull().And.BeEmpty();
        page.Sections.Should().NotBeNull().And.BeEmpty();
    }
}

public class PageSectionTests
{
    [Fact]
    public void PageSection_DefaultValues_ShouldBeCorrect()
    {
        var section = new PageSection();

        section.SectionType.Should().Be(SectionType.Hero);
        section.Heading.Should().BeNull();
        section.Subheading.Should().BeNull();
        section.Data.Should().BeNull();
        section.SortOrder.Should().Be(0);
        section.IsVisible.Should().BeTrue();
        section.CssClasses.Should().BeNull();
    }

    [Fact]
    public void PageSection_ShouldSetProperties()
    {
        var pageId = Guid.NewGuid();
        var section = new PageSection
        {
            PageId = pageId,
            SectionType = SectionType.Pricing,
            Heading = "Our Plans",
            Subheading = "Choose your plan",
            Data = "{\"plans\":[]}",
            SortOrder = 3,
            IsVisible = false,
            CssClasses = "bg-dark text-white"
        };

        section.PageId.Should().Be(pageId);
        section.SectionType.Should().Be(SectionType.Pricing);
        section.Heading.Should().Be("Our Plans");
        section.IsVisible.Should().BeFalse();
    }
}

public class ContentResourceTests
{
    [Fact]
    public void ContentResource_DefaultValues_ShouldBeCorrect()
    {
        var cr = new ContentResource();

        cr.Slug.Should().Be(string.Empty);
        cr.Title.Should().Be(string.Empty);
        cr.Summary.Should().BeNull();
        cr.Body.Should().BeNull();
        cr.ResourceType.Should().Be(ContentResourceType.Article);
        cr.Status.Should().Be(ContentResourceStatus.Draft);
        cr.ViewCount.Should().Be(0);
        cr.IsFeatured.Should().BeFalse();
        cr.SortOrder.Should().Be(0);
    }

    [Fact]
    public void ContentResource_ShouldSetAllProperties()
    {
        var authorId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        var cr = new ContentResource
        {
            Slug = "intro-csharp",
            Title = "Introduction to C#",
            Summary = "Learn C# basics",
            Body = "<p>Hello World</p>",
            ResourceType = ContentResourceType.Tutorial,
            Status = ContentResourceStatus.Published,
            Locale = "en",
            CategorySlug = "programming",
            Tags = "csharp,dotnet",
            AuthorId = authorId,
            AuthorName = "John Doe",
            CoverImageUrl = "https://img.com/cover.png",
            VideoUrl = "https://video.com/intro",
            DownloadUrl = "https://dl.com/file.zip",
            ExternalUrl = "https://ext.com/resource",
            LinkedEntityId = linkedId,
            LinkedEntityType = "Course",
            MetaTitle = "Intro to C#",
            MetaDescription = "A beginner tutorial",
            OgImageUrl = "https://img.com/og.png",
            ReadingTimeMinutes = 15,
            ViewCount = 100,
            IsFeatured = true,
            SortOrder = 2
        };

        cr.Slug.Should().Be("intro-csharp");
        cr.ResourceType.Should().Be(ContentResourceType.Tutorial);
        cr.Status.Should().Be(ContentResourceStatus.Published);
        cr.AuthorId.Should().Be(authorId);
        cr.ViewCount.Should().Be(100);
        cr.IsFeatured.Should().BeTrue();
        cr.LinkedEntityType.Should().Be("Course");
    }
}

#endregion

#region Enum Tests

public class PageTypeEnumTests
{
    [Fact]
    public void PageType_ShouldHave5Values()
    {
        Enum.GetValues<PageType>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(PageType.Landing, 0)]
    [InlineData(PageType.Legal, 1)]
    [InlineData(PageType.ResourceIndex, 2)]
    [InlineData(PageType.Resource, 3)]
    [InlineData(PageType.Custom, 4)]
    public void PageType_Values(PageType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

public class PageStatusEnumTests
{
    [Fact]
    public void PageStatus_ShouldHave3Values()
    {
        Enum.GetValues<PageStatus>().Should().HaveCount(3);
    }
}

public class SectionTypeEnumTests
{
    [Fact]
    public void SectionType_ShouldHave15Values()
    {
        Enum.GetValues<SectionType>().Should().HaveCount(15);
    }

    [Fact]
    public void SectionType_FirstAndLast()
    {
        ((int)SectionType.Hero).Should().Be(0);
        ((int)SectionType.Custom).Should().Be(14);
    }
}

public class ContentResourceTypeEnumTests
{
    [Fact]
    public void ContentResourceType_ShouldHave8Values()
    {
        Enum.GetValues<ContentResourceType>().Should().HaveCount(8);
    }
}

public class ContentResourceStatusEnumTests
{
    [Fact]
    public void ContentResourceStatus_ShouldHave4Values()
    {
        Enum.GetValues<ContentResourceStatus>().Should().HaveCount(4);
    }
}

#endregion

#region Mapping Extensions Tests

public class MappingExtensionsTests
{
    [Fact]
    public void Page_ToDto_ShouldMapAllProperties()
    {
        var page = new Page
        {
            Slug = "test-page",
            Title = "Test Page",
            Description = "A test page",
            PageType = PageType.Legal,
            Status = PageStatus.Published,
            Locale = "en",
            MetaTitle = "Meta Title",
            MetaDescription = "Meta Desc",
            OgTitle = "OG Title",
            Body = "<p>Content</p>",
            SortOrder = 2
        };

        var dto = page.ToDto();

        dto.Slug.Should().Be("test-page");
        dto.Title.Should().Be("Test Page");
        dto.PageType.Should().Be("Legal");
        dto.Status.Should().Be("Published");
        dto.Locale.Should().Be("en");
        dto.MetaTitle.Should().Be("Meta Title");
        dto.OgTitle.Should().Be("OG Title");
        dto.Body.Should().Be("<p>Content</p>");
        dto.SortOrder.Should().Be(2);
    }

    [Fact]
    public void Page_ToDto_ShouldMapSections()
    {
        var pageId = Guid.NewGuid();
        var page = new Page
        {
            Slug = "with-sections",
            Title = "Page With Sections"
        };
        page.Sections.Add(new PageSection
        {
            PageId = pageId,
            SectionType = SectionType.Hero,
            Heading = "Welcome",
            SortOrder = 1
        });
        page.Sections.Add(new PageSection
        {
            PageId = pageId,
            SectionType = SectionType.Features,
            Heading = "Features",
            SortOrder = 0
        });

        var dto = page.ToDto();

        dto.Sections.Should().HaveCount(2);
        dto.Sections[0].SortOrder.Should().Be(0); // sorted by SortOrder
        dto.Sections[1].SortOrder.Should().Be(1);
    }

    [Fact]
    public void PageSection_ToDto_ShouldMapAllProperties()
    {
        var section = new PageSection
        {
            SectionType = SectionType.Faq,
            Heading = "FAQ",
            Subheading = "Common questions",
            Data = "{\"items\":[]}",
            SortOrder = 5,
            IsVisible = false,
            CssClasses = "faq-section"
        };

        var dto = section.ToDto();

        dto.SectionType.Should().Be("Faq");
        dto.Heading.Should().Be("FAQ");
        dto.Subheading.Should().Be("Common questions");
        dto.Data.Should().Be("{\"items\":[]}");
        dto.SortOrder.Should().Be(5);
        dto.IsVisible.Should().BeFalse();
        dto.CssClasses.Should().Be("faq-section");
    }

    [Fact]
    public void ContentResource_ToDto_ShouldMapAllProperties()
    {
        var cr = new ContentResource
        {
            Slug = "tutorial-1",
            Title = "Tutorial 1",
            Summary = "First tutorial",
            ResourceType = ContentResourceType.Video,
            Status = ContentResourceStatus.Published,
            ViewCount = 500,
            IsFeatured = true,
            ReadingTimeMinutes = 10
        };

        var dto = cr.ToDto();

        dto.Slug.Should().Be("tutorial-1");
        dto.ResourceType.Should().Be("Video");
        dto.Status.Should().Be("Published");
        dto.ViewCount.Should().Be(500);
        dto.IsFeatured.Should().BeTrue();
        dto.ReadingTimeMinutes.Should().Be(10);
    }

    [Fact]
    public void ToDtos_Page_ShouldMapCollection()
    {
        var pages = new List<Page>
        {
            new() { Slug = "p1", Title = "Page 1" },
            new() { Slug = "p2", Title = "Page 2" }
        };

        var dtos = pages.ToDtos().ToList();

        dtos.Should().HaveCount(2);
        dtos[0].Slug.Should().Be("p1");
        dtos[1].Slug.Should().Be("p2");
    }

    [Fact]
    public void ToDtos_ContentResource_ShouldMapCollection()
    {
        var resources = new List<ContentResource>
        {
            new() { Slug = "r1", Title = "Resource 1" },
            new() { Slug = "r2", Title = "Resource 2" }
        };

        var dtos = resources.ToDtos().ToList();

        dtos.Should().HaveCount(2);
    }

    [Fact]
    public void Page_ToOpenGraphDto_ShouldUseFallbackChain()
    {
        // When OgTitle is null, should fall back to MetaTitle, then Title
        var page = new Page
        {
            Slug = "test",
            Title = "Page Title",
            Description = "Page Description",
            // OgTitle is null, MetaTitle is null — should use Title
        };

        var og = page.ToOpenGraphDto();

        og.Title.Should().Be("Page Title");
        og.OgTitle.Should().Be("Page Title");
        og.Description.Should().Be("Page Description");
        og.OgType.Should().Be("website"); // default fallback
        og.TwitterCard.Should().Be("summary_large_image"); // default fallback
    }

    [Fact]
    public void Page_ToOpenGraphDto_ShouldPreferOgFields()
    {
        var page = new Page
        {
            Slug = "test",
            Title = "Page Title",
            MetaTitle = "Meta Title",
            OgTitle = "OG Title",
            Description = "Desc",
            MetaDescription = "Meta Desc",
            OgDescription = "OG Desc",
            OgType = "article",
            TwitterCard = "summary"
        };

        var og = page.ToOpenGraphDto();

        og.Title.Should().Be("OG Title");
        og.OgTitle.Should().Be("OG Title");
        og.Description.Should().Be("OG Desc");
        og.OgType.Should().Be("article");
        og.TwitterCard.Should().Be("summary");
    }

    [Fact]
    public void Page_ToOpenGraphDto_ShouldFallbackToMetaTitle()
    {
        var page = new Page
        {
            Slug = "test",
            Title = "Page Title",
            MetaTitle = "Meta Title",
            // OgTitle is null — should use MetaTitle
        };

        var og = page.ToOpenGraphDto();

        og.Title.Should().Be("Meta Title");
        og.OgTitle.Should().Be("Meta Title");
    }

    [Fact]
    public void ContentResource_ToOpenGraphDto_ShouldMapFields()
    {
        var cr = new ContentResource
        {
            Slug = "article-1",
            Title = "Article Title",
            Summary = "Article Summary",
            CoverImageUrl = "https://img.com/cover.png"
        };

        var og = cr.ToOpenGraphDto();

        og.Slug.Should().Be("article-1");
        og.Title.Should().Be("Article Title");
        og.Description.Should().Be("Article Summary");
        og.OgImageUrl.Should().Be("https://img.com/cover.png"); // falls back to CoverImageUrl
        og.OgType.Should().Be("article"); // always "article" for resources
        og.TwitterCard.Should().Be("summary_large_image");
    }

    [Fact]
    public void ContentResource_ToOpenGraphDto_ShouldPreferMetaFields()
    {
        var cr = new ContentResource
        {
            Slug = "article-1",
            Title = "Title",
            MetaTitle = "Meta Title",
            Summary = "Summary",
            MetaDescription = "Meta Description",
            OgImageUrl = "https://img.com/og.png",
            CoverImageUrl = "https://img.com/cover.png"
        };

        var og = cr.ToOpenGraphDto();

        og.Title.Should().Be("Meta Title");
        og.Description.Should().Be("Meta Description");
        og.OgImageUrl.Should().Be("https://img.com/og.png"); // prefers OgImageUrl
    }
}

#endregion

#region Marketing Lead Service Tests

public class MarketingLeadServiceTests
{
    [Fact]
    public async Task ListAsync_MixedCaseFilters_ReturnsMatchingLead()
    {
        await using var db = CreateDbContext();
        db.Set<MarketingLead>().AddRange(
            new MarketingLead
            {
                Source = MarketingLeadSources.Contact,
                Status = MarketingLeadStatuses.Reviewed,
                Topic = MarketingLeadTopics.Sales,
                Email = "sales@example.com"
            },
            new MarketingLead
            {
                Source = MarketingLeadSources.Newsletter,
                Status = MarketingLeadStatuses.New,
                Topic = MarketingLeadTopics.Support,
                Email = "newsletter@example.com"
            });
        await db.SaveChangesAsync();

        var service = new MarketingLeadService(db, new RecordingContentPagesAuditEventSink());

        var results = await service.ListAsync(" CONTACT ", " REVIEWED ", " SALES ", null, 0, 10);

        results.Should().ContainSingle();
        results[0].Email.Should().Be("sales@example.com");
    }

    private static TestContentPagesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestContentPagesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestContentPagesDbContext(options);
    }

    private sealed class TestContentPagesDbContext(DbContextOptions<TestContentPagesDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MarketingLead>();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}

#endregion

#region DTO Tests

public class DtoDefaultTests
{
    [Fact]
    public void PageDto_Defaults()
    {
        var dto = new PageDto();
        dto.Slug.Should().Be(string.Empty);
        dto.Title.Should().Be(string.Empty);
        dto.Sections.Should().BeEmpty();
    }

    [Fact]
    public void PageSectionDto_Defaults()
    {
        var dto = new PageSectionDto();
        dto.IsVisible.Should().BeFalse(); // no default in record
    }

    [Fact]
    public void CreatePageSectionDto_IsVisible_DefaultsToTrue()
    {
        var dto = new CreatePageSectionDto();
        dto.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void CreateContentResourceDto_Defaults()
    {
        var dto = new CreateContentResourceDto();
        dto.Slug.Should().Be(string.Empty);
        dto.Title.Should().Be(string.Empty);
        dto.ResourceType.Should().Be(ContentResourceType.Article);
    }

    [Fact]
    public void OpenGraphMetadataDto_Defaults()
    {
        var dto = new OpenGraphMetadataDto();
        dto.Slug.Should().Be(string.Empty);
        dto.Title.Should().Be(string.Empty);
    }
}

#endregion
