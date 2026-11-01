using Microsoft.AspNetCore.SignalR.Client;
using Core;

namespace CLI;

public static class ConnectionManager
{

    public static HubConnection Connection { get; }

    static ConnectionManager()
    {
        try
        {
            Connection = new HubConnectionBuilder()
                .WithUrl("http://127.0.0.1:5000/hub")
                .WithAutomaticReconnect()
                .Build();

            Task.WaitAll([Connection.StartAsync()]);
        }
        catch
        {
            ConsoleUtils.Error("Connection refused. Make sure the FreeBlock daemon is running.");
            Environment.Exit(1);
        }
    }

    public static async Task<Result> SafeInvokeAsync(string method, params object?[] args)
    {
        try
        {
            await Connection!.InvokeCoreAsync(method, args);
            return new Success();
        }
        catch (Exception e)
        {
            return new Failure($"The daemon returned an error: {e}");
        }
    }

    public static async Task<Result<T>> SafeInvokeAsync<T>(string method)
    {
        try
        {
            return new Success<T>(await Connection!.InvokeAsync<T>(method));
        }
        catch (Exception e)
        {
            return new Failure<T>($"The daemon returned an error: {e}");
        }
    }

    public static async Task<Result<T>> SafeInvokeAsync<T>(string method, object? arg1)
    {
        try
        {
            return new Success<T>(await Connection!.InvokeAsync<T>(method, arg1));
        }
        catch (Exception e)
        {
            return new Failure<T>($"The daemon returned an error: {e}");
        }
    }

    public static async Task<Result<T>> SafeInvokeAsync<T>(string method, object? arg1, object? arg2)
    {
        try
        {
            return new Success<T>(await Connection!.InvokeAsync<T>(method, arg1, arg2));
        }
        catch (Exception e)
        {
            return new Failure<T>($"The daemon returned an error: {e}");
        }
    }

}

public static class InvokeHelpers
{
    public static Task<Result> Invoke(string method)
        => ConnectionManager.SafeInvokeAsync(method);

    public static Task<Result> Invoke(string method, object? arg1)
        => ConnectionManager.SafeInvokeAsync(method, arg1);

    public static Task<Result> Invoke(string method, object? arg1, object? arg2)
        => ConnectionManager.SafeInvokeAsync(method, arg1, arg2);

    public static Task<Result> Invoke(string method, object? arg1, object? arg2, object? arg3)
        => ConnectionManager.SafeInvokeAsync(method, arg1, arg2, arg3);

    public static Task<Result<T>> Invoke<T>(string method)
        => ConnectionManager.SafeInvokeAsync<T>(method);

    public static Task<Result<T>> Invoke<T>(string method, object? arg1)
        => ConnectionManager.SafeInvokeAsync<T>(method, arg1);

    public static Task<Result<T>> Invoke<T>(string method, object? arg1, object? arg2)
        => ConnectionManager.SafeInvokeAsync<T>(method, arg1, arg2);
}
