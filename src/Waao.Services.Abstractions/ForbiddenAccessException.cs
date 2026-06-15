namespace Waao.Services.Abstractions;

/// <summary>
/// Thrown when the caller is authenticated but not allowed to perform the action on the
/// target resource (e.g. a peer trying to manage someone else's skills, or a subject trying
/// to read their own private manager notes). Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);
