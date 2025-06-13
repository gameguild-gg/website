using MediatR;
using System.ComponentModel.DataAnnotations;

namespace GameGuild.Common.Behaviors;

/// <summary>
/// Pipeline behavior for validating requests using DataAnnotations
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Basic validation using DataAnnotations
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        
        bool isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

        if (!isValid)
        {
            string errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Validation failed: {errors}");
        }

        return await next();
    }
}
