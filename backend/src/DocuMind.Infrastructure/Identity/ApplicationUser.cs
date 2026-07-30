using Microsoft.AspNetCore.Identity;

namespace DocuMind.Infrastructure.Identity;

/// <summary>
/// The DocuMind user identity. Intentionally empty: no custom profile fields are needed yet, but
/// the type is introduced now — rather than using <see cref="IdentityUser{TKey}"/> directly — so
/// a future profile addition (e.g. a display name) does not force a later rename that would touch
/// every reference, migration, and foreign key across the codebase.
/// </summary>
/// <remarks>
/// This lives in Infrastructure, not Domain, because it derives from an ASP.NET Core Identity type
/// and is therefore a persistence concern rather than a domain concept. Placing it in Domain would
/// have required a framework package reference in the innermost layer to host a type that layer
/// never consumes: document ownership is expressed as a bare <see cref="Guid"/> on the entity, and
/// the foreign key is configured here in <c>DocuMindDbContext</c>. That is unlike the deliberate
/// <c>Pgvector</c> reference in Domain, which is forced — EF Core can only translate
/// <c>CosineDistance</c> to SQL when the entity property is typed as a vector.
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>
{
}
