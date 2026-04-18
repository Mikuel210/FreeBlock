using Microsoft.AspNetCore.SignalR.Client;

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
            ConsoleUtils.Error("Connection refused. Make sure the FreeBlock background service is running.");
            Environment.Exit(0);
        }
    }

}
