using System;

namespace GameGuild;

/// <summary>
///     Marks an entity property as sensitive for automatic change auditing. Automatic
///     audit capture stores the property name but replaces its value with a constant
///     redaction marker, so secrets such as password hashes or token values never enter
///     audit storage. Platform-generic: any module may apply it to its own properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AuditSensitiveAttribute : Attribute
{
}
