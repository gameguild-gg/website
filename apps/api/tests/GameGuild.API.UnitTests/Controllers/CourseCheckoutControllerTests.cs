using System.Security.Claims;
using FluentAssertions;
using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class CourseCheckoutControllerTests
{
    [Fact]
    public async Task CompleteCheckout_WhenProductIsLinked_GrantsEntitlementAndCourseAccess()
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var entitlementId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var course = new GameGuild.Learning.Courses.Program
        {
            Id = courseId,
            Title = "Paid course",
            Slug = "paid-course",
            Status = ContentStatus.Published,
            Visibility = ContentVisibility.Public,
            EnrollmentStatus = EnrollmentStatus.Open,
        };
        var product = Product.Create("Paid course access", ProductType.Course);
        product.Id = productId;
        var (pricing, _) = ProductPricing.CreateWithVersion(productId, "Standard", 49m, "USD", null, null, null, true);
        product.Pricing.Add(pricing);
        var userProduct = UserProduct.Create(userId, productId, ProductAcquisitionType.Purchase, 49m, "USD");
        userProduct.Id = entitlementId;

        var programService = new Mock<IProgramCrudService>();
        programService.Setup(service => service.GetProgramByIdAsync(courseId)).ReturnsAsync(course);
        programService.Setup(service => service.GetLinkedProductsAsync(courseId)).ReturnsAsync(new[] { productId });
        programService
            .Setup(service => service.AddUserToProgramAsync(courseId, userId))
            .ReturnsAsync(new UserProgressDto(Guid.NewGuid(), courseId, userId, 0, null, null, null, []));

        var enrollmentService = new Mock<IProgramEnrollmentService>();
        enrollmentService
            .Setup(service => service.AutoEnrollInProductProgramsAsync(userId, productId))
            .ReturnsAsync(new[] { new ProgramEnrollment { Id = enrollmentId, ProgramId = courseId, UserId = userId } });

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService
            .Setup(service => service.GrantEntitlementAsync(
                userId,
                productId,
                ProductAcquisitionType.Purchase,
                49m,
                "USD",
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntitlementResult.Succeeded(userProduct));

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(repository => repository.GetByIdAsync(productId, It.IsAny<CancellationToken>(), true, false, true))
            .ReturnsAsync(product);

        var controller = CreateController(userId, programService.Object, enrollmentService.Object, entitlementService.Object, productRepository.Object);

        var result = await controller.CompleteCheckout(
            courseId,
            new CompleteCourseCheckoutRequest(productId, "checkout-session-1", "test_card"),
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CompleteCourseCheckoutResponse>().Subject;
        response.CourseId.Should().Be(courseId);
        response.ProductId.Should().Be(productId);
        response.EntitlementId.Should().Be(entitlementId);
        response.EnrollmentIds.Should().Equal(enrollmentId);
        response.Amount.Should().Be(49m);
        response.Currency.Should().Be("USD");
        response.LearningUrl.Should().Be("/courses/paid-course/content");
        response.PaymentProviderReference.Should().Be("checkout-session-1");
        programService.Verify(service => service.AddUserToProgramAsync(courseId, userId), Times.Once);
    }

    [Fact]
    public async Task CompleteCheckout_WhenProductIsNotLinked_DoesNotGrantEntitlement()
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var programService = new Mock<IProgramCrudService>();
        programService.Setup(service => service.GetProgramByIdAsync(courseId)).ReturnsAsync(new GameGuild.Learning.Courses.Program
        {
            Id = courseId,
            Title = "Paid course",
            Status = ContentStatus.Published,
            Visibility = ContentVisibility.Public,
            EnrollmentStatus = EnrollmentStatus.Open,
        });
        programService.Setup(service => service.GetLinkedProductsAsync(courseId)).ReturnsAsync([]);
        var enrollmentService = new Mock<IProgramEnrollmentService>();
        var entitlementService = new Mock<IEntitlementService>();
        var productRepository = new Mock<IProductRepository>();
        var controller = CreateController(userId, programService.Object, enrollmentService.Object, entitlementService.Object, productRepository.Object);

        var result = await controller.CompleteCheckout(courseId, new CompleteCourseCheckoutRequest(productId), CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ProblemDetails>().Which.Title.Should().Be("Product is not linked to course");
        entitlementService.Verify(
            service => service.GrantEntitlementAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ProductAcquisitionType>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CourseCheckoutController CreateController(
        Guid userId,
        IProgramCrudService programService,
        IProgramEnrollmentService enrollmentService,
        IEntitlementService entitlementService,
        IProductRepository productRepository)
    {
        var handler = new CompleteCourseCheckoutCommandHandler(
            programService.Object,
            enrollmentService.Object,
            entitlementService.Object,
            productRepository.Object);
        return new CourseCheckoutController(new DirectSender(handler))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "test")),
                },
            },
        };
    }

    private sealed class DirectSender(CompleteCourseCheckoutCommandHandler handler) : ISender
    {
        public async Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            if (request is CompleteCourseCheckoutCommand command)
                return (TResponse)(object)await handler.Handle(command, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Unsupported request type: {request.GetType().FullName}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new InvalidOperationException($"Unsupported request type: {request.GetType().FullName}");

        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            if (request is CompleteCourseCheckoutCommand command)
                return await handler.Handle(command, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Unsupported request type: {request.GetType().FullName}");
        }
    }
}
