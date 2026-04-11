using Daemon;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSignalR();

var app = builder.Build();
app.MapHub<CommunicationHub>("/hub");
app.Map("/", () => "FreeBlock: All systems nominal");
app.Run();
