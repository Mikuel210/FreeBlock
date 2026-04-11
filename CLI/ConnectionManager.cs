using Microsoft.AspNetCore.SignalR.Client;

namespace CLI;

public static class ConnectionManager
{

    public static HubConnection? Connection { get; private set; }

    public static async Task Initialize()
    {
        Connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/hub")
            .WithAutomaticReconnect()
            .Build();

        await Connection.StartAsync();
    }

}
