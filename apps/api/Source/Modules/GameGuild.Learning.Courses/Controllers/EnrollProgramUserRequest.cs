using System.ComponentModel.DataAnnotations;

namespace GameGuild.Learning.Courses;

public sealed record EnrollProgramUserRequest(
  [Required, MinLength(1), MaxLength(320)] string UserReference);
