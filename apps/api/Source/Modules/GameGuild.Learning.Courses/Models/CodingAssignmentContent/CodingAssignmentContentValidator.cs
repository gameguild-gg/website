using System.Text.RegularExpressions;
using FluentValidation;

namespace GameGuild.Learning.Courses;

/// <summary>
/// FluentValidation rules for <see cref="CodingAssignmentContent"/> (v1).
/// </summary>
public sealed class CodingAssignmentContentValidator : AbstractValidator<CodingAssignmentContent>
{
    private static readonly HashSet<string> s_validLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "cpp", "c", "sdl-cpp", "raylib-cpp", "allegro-cpp"
    };

    private static readonly HashSet<string> s_validVisibilities = new(StringComparer.Ordinal)
    {
        "Public", "Private"
    };

    private static readonly HashSet<string> s_validParamTypes = new(StringComparer.Ordinal)
    {
        nameof(FunctionParameterType.String),
        nameof(FunctionParameterType.Boolean),
        nameof(FunctionParameterType.Integer),
        nameof(FunctionParameterType.Float),
    };

    // C identifier — bounds C++ name mangling to a portable subset.
    private static readonly Regex s_functionName = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public CodingAssignmentContentValidator()
    {
        RuleFor(x => x.Type)
            .Equal("coding-assignment").WithErrorCode("invalid_type");

        RuleFor(x => x.Version)
            .Equal(1).WithErrorCode("invalid_version");

        RuleFor(x => x.Environment.Language)
            .Must(lang => s_validLanguages.Contains(lang ?? string.Empty))
            .WithMessage("Language must be one of: cpp, c, sdl-cpp, raylib-cpp, allegro-cpp")
            .WithErrorCode("invalid_language")
            .When(x => x.Environment != null);

        // Per-test Weight >= 0
        RuleForEach(x => x.Tests.Public)
            .Must(t => t != null && t.Weight >= 0)
            .WithMessage("Test Weight must be non-negative").WithErrorCode("weight_non_negative")
            .When(x => x.Tests?.Public != null);

        RuleForEach(x => x.Tests.Private)
            .Must(t => t != null && t.Weight >= 0)
            .WithMessage("Test Weight must be non-negative").WithErrorCode("weight_non_negative")
            .When(x => x.Tests?.Private != null);

        // Per-file: Visibility ∈ {Public, Private} — reject "Solution" (Must-NOT-Have enforced server-side)
        RuleForEach(x => x.Data.Files.Values)
            .Must(f => f != null && s_validVisibilities.Contains(f.Visibility ?? string.Empty))
            .WithMessage("File Visibility must be Public or Private").WithErrorCode("invalid_visibility_value")
            .When(x => x.Data?.Files != null);

        // Per-file: Private + Modifiable makes no sense (file is hidden from learner but learner can edit?)
        RuleForEach(x => x.Data.Files.Values)
            .Must(f => f == null || !(f.Visibility == "Private" && f.Modifiable))
            .WithMessage("Private files cannot be Modifiable").WithErrorCode("private_file_not_modifiable")
            .When(x => x.Data?.Files != null);

        // Total serialized size budget (base64 images included) — 10MB
        RuleFor(x => x.Data.Files)
            .Must(files => files.Values.Sum(f => f?.Content?.Length ?? 0) <= 10_000_000)
            .WithMessage("Total assignment size must not exceed 10MB")
            .WithErrorCode("files_too_large")
            .When(x => x.Data?.Files != null);

        // FunctionalTestGroup: FunctionName must match C-identifier regex
        RuleForEach(x => x.Tests.Public)
            .Must(IsValidFunctionName)
            .WithMessage("FunctionName must match ^[A-Za-z_][A-Za-z0-9_]*$")
            .WithErrorCode("invalid_function_name")
            .When(x => x.Tests?.Public != null);

        RuleForEach(x => x.Tests.Private)
            .Must(IsValidFunctionName)
            .WithMessage("FunctionName must match ^[A-Za-z_][A-Za-z0-9_]*$")
            .WithErrorCode("invalid_function_name")
            .When(x => x.Tests?.Private != null);

        // FunctionalTestGroup: ≥1 case per group
        RuleForEach(x => x.Tests.Public)
            .Must(HasAtLeastOneCase)
            .WithMessage("FunctionalTestGroup must have at least one case")
            .WithErrorCode("at_least_one_case")
            .When(x => x.Tests?.Public != null);

        RuleForEach(x => x.Tests.Private)
            .Must(HasAtLeastOneCase)
            .WithMessage("FunctionalTestGroup must have at least one case")
            .WithErrorCode("at_least_one_case")
            .When(x => x.Tests?.Private != null);

        // FunctionalTestGroup: each case Inputs.Length == Function.Parameters.Length
        RuleForEach(x => x.Tests.Public)
            .Must(AreCaseInputLengthsValid)
            .WithMessage("FunctionalTestCase Inputs length must match Function.Parameters length")
            .WithErrorCode("case_inputs_length_mismatch")
            .When(x => x.Tests?.Public != null);

        RuleForEach(x => x.Tests.Private)
            .Must(AreCaseInputLengthsValid)
            .WithMessage("FunctionalTestCase Inputs length must match Function.Parameters length")
            .WithErrorCode("case_inputs_length_mismatch")
            .When(x => x.Tests?.Private != null);

        // FunctionalTestGroup: signature + per-case types limited to v1 set (String/Boolean/Integer/Float)
        RuleForEach(x => x.Tests.Public)
            .Must(AreFunctionalParamTypesValid)
            .WithMessage("FunctionalTestGroup parameter type not supported in v1")
            .WithErrorCode("functional_param_type_not_supported_v1")
            .When(x => x.Tests?.Public != null);

        RuleForEach(x => x.Tests.Private)
            .Must(AreFunctionalParamTypesValid)
            .WithMessage("FunctionalTestGroup parameter type not supported in v1")
            .WithErrorCode("functional_param_type_not_supported_v1")
            .When(x => x.Tests?.Private != null);

        // Grading rules
        RuleFor(x => x.Grading.MaxScore)
            .GreaterThan(0).WithMessage("MaxScore must be greater than 0").WithErrorCode("max_score_positive")
            .When(x => x.Grading != null);
    }

    private static bool IsValidFunctionName(Test? t)
    {
        if (t is not FunctionalTestGroup f)
        {
            return true;
        }

        return f.Function != null
            && !string.IsNullOrEmpty(f.Function.FunctionName)
            && s_functionName.IsMatch(f.Function.FunctionName);
    }

    private static bool HasAtLeastOneCase(Test? t)
    {
        if (t is not FunctionalTestGroup g)
        {
            return true;
        }

        if (g.Cases == null)
        {
            return false;
        }

        return g.Cases.Count > 0;
    }

    private static bool AreCaseInputLengthsValid(Test? t)
    {
        if (t is not FunctionalTestGroup g)
        {
            return true;
        }

        if (g.Function?.Parameters == null || g.Cases == null)
        {
            return false;
        }

        var expected = g.Function.Parameters.Count;
        foreach (var c in g.Cases)
        {
            if (c?.Inputs == null || c.Inputs.Length != expected)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AreFunctionalParamTypesValid(Test? t)
    {
        if (t is not FunctionalTestGroup f)
        {
            return true;
        }

        if (f.Function?.ReturnType == null || f.Function.Parameters == null || f.Cases == null)
        {
            return false;
        }

        if (!IsValidParamType(f.Function.ReturnType.Type))
        {
            return false;
        }

        foreach (var p in f.Function.Parameters)
        {
            if (p != null && !IsValidParamType(p.Type))
            {
                return false;
            }
        }
        foreach (var c in f.Cases)
        {
            if (c?.Expected == null || c.Inputs == null || !IsValidParamType(c.Expected.Type))
            {
                return false;
            }

            foreach (var i in c.Inputs)
            {
                if (i != null && !IsValidParamType(i.Type))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsValidParamType(FunctionParameterType t) =>
        s_validParamTypes.Contains(t.ToString());
}
