using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Courses;

public sealed record CompleteCourseCheckoutCommand(
    Guid CourseId,
    Guid UserId,
    Guid ProductId,
    string? PaymentProviderReference) : ICommand<CompleteCourseCheckoutOutcome>;

public sealed record CompleteCourseCheckoutOutcome(
    CompleteCourseCheckoutResponse? Response,
    int StatusCode,
    ProblemDetails? Problem)
{
    public static CompleteCourseCheckoutOutcome Success(CompleteCourseCheckoutResponse response) =>
        new(response, StatusCodes.Status200OK, null);

    public static CompleteCourseCheckoutOutcome Failure(int statusCode, string title, string detail) =>
        new(null, statusCode, new ProblemDetails { Title = title, Detail = detail });
}

public sealed class CompleteCourseCheckoutCommandHandler(
    IProgramCrudService programService,
    IProgramEnrollmentService enrollmentService,
    IEntitlementService entitlementService,
    IProductRepository productRepository)
    : ICommandHandler<CompleteCourseCheckoutCommand, CompleteCourseCheckoutOutcome>
{
    public async Task<CompleteCourseCheckoutOutcome> Handle(
        CompleteCourseCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
            return CompleteCourseCheckoutOutcome.Failure(
                StatusCodes.Status400BadRequest,
                "Product required",
                "A course product must be selected before checkout can complete.");

        var course = await programService.GetProgramByIdAsync(request.CourseId).ConfigureAwait(false);
        if (course == null || course.Status != ContentStatus.Published || course.Visibility != ContentVisibility.Public)
            return CompleteCourseCheckoutOutcome.Failure(
                StatusCodes.Status404NotFound,
                "Course not found",
                "The selected course is not available for checkout.");

        if (!course.IsEnrollmentOpen)
            return CompleteCourseCheckoutOutcome.Failure(
                StatusCodes.Status409Conflict,
                "Enrollment closed",
                "This course is not currently open for checkout enrollment.");

        var linkedProducts = (await programService.GetLinkedProductsAsync(request.CourseId).ConfigureAwait(false)).ToHashSet();
        if (!linkedProducts.Contains(request.ProductId))
            return CompleteCourseCheckoutOutcome.Failure(
                StatusCodes.Status400BadRequest,
                "Product is not linked to course",
                "The selected product does not grant access to this course.");

        var product = await productRepository.GetByIdAsync(
            request.ProductId,
            cancellationToken,
            includePricing: true,
            isPublished: true).ConfigureAwait(false);
        if (product == null)
            return CompleteCourseCheckoutOutcome.Failure(
                StatusCodes.Status404NotFound,
                "Product not found",
                "The selected course product is not available for checkout.");

        var pricing = product.Pricing.FirstOrDefault(entry => entry.IsDefault) ?? product.Pricing.FirstOrDefault();
        var amount = pricing?.GetCurrentPrice() ?? 0m;
        var currency = pricing?.Currency ?? "USD";
        var acquisitionType = amount <= 0
            ? ProductAcquisitionType.Free
            : product.Type == ProductType.Subscription
                ? ProductAcquisitionType.Subscription
                : ProductAcquisitionType.Purchase;

        var entitlement = await entitlementService.GrantEntitlementAsync(
            request.UserId,
            request.ProductId,
            acquisitionType,
            amount,
            currency,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!entitlement.Success || entitlement.UserProduct == null)
            return CompleteCourseCheckoutOutcome.Failure(
                StatusCodes.Status400BadRequest,
                "Entitlement could not be granted",
                entitlement.ErrorMessage ?? "The course product could not be attached to the current learner.");

        var productEnrollments = (await enrollmentService
            .AutoEnrollInProductProgramsAsync(request.UserId, request.ProductId)
            .ConfigureAwait(false)).ToList();
        var progressRows = new List<UserProgressDto>();
        foreach (var enrollment in productEnrollments)
        {
            var progress = await programService
                .AddUserToProgramAsync(enrollment.ProgramId, request.UserId)
                .ConfigureAwait(false);
            if (progress != null) progressRows.Add(progress);
        }

        if (progressRows.All(progress => progress.CourseId != request.CourseId))
        {
            var progress = await programService
                .AddUserToProgramAsync(request.CourseId, request.UserId)
                .ConfigureAwait(false);
            if (progress != null) progressRows.Add(progress);
        }

        return CompleteCourseCheckoutOutcome.Success(new CompleteCourseCheckoutResponse(
            request.CourseId,
            request.ProductId,
            entitlement.UserProduct.Id,
            productEnrollments.Select(enrollment => enrollment.Id).ToArray(),
            entitlement.AlreadyHadAccess,
            amount,
            currency,
            course.Slug == null ? $"/courses/{request.CourseId}/content" : $"/courses/{course.Slug}/content",
            request.PaymentProviderReference));
    }
}
