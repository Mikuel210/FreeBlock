namespace Core;

public record Result;
public record Success() : Result;
public record Failure(string Message) : Result;

public record Result<T>;
public record Success<T>(T Value) : Result<T>;
public record Failure<T>(string Message) : Result<T>;
