using Microsoft.AspNetCore.SignalR;

namespace server.Hubs
{
    public class ProgressHub : Hub
    {
        public async Task SendProgress(string message)
        {
            Console.WriteLine("hello");
            await Clients.All.SendAsync("ReceiveProgress", message);
        }
    }
}
