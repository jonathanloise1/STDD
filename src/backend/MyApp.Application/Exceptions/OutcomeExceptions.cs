namespace MyApp.Application.Exceptions;

/// <summary>The user lost access to the resource (removed, role changed). HTTP 403.</summary>
public class AccessLostException(string key, string detail) : OutcomeException(key, 403, detail);

/// <summary>The resource does not exist (any more). HTTP 404.</summary>
public class ResourceNotFoundException(string key, string detail) : OutcomeException(key, 404, detail);

/// <summary>The request conflicts with the current state of the data. HTTP 409.</summary>
public class ConflictException(string key, string detail) : OutcomeException(key, 409, detail);

/// <summary>The request is invalid; carries the field errors. HTTP 400.</summary>
public class RequestValidationException(string key, string detail, IReadOnlyDictionary<string, string[]>? errors = null)
    : OutcomeException(key, 400, detail, errors);

/// <summary>A dependency (database, external service) is not available. HTTP 503.</summary>
public class DependencyUnavailableException(string key, string detail) : OutcomeException(key, 503, detail);
