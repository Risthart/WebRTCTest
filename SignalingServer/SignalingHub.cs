using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace WebRtcClientMvp;

public class SignalingHub : Hub
{
    private const string JoinedToKey = "joinedTo";
    private static readonly ConcurrentDictionary<string, string> SerialIdConnectionId = new();
    
    private readonly IHubContext<SignalingHub> _hubContext;

    public SignalingHub(IHubContext<SignalingHub> hubContext, ILogger<SignalingHub> logger)
    {
        _hubContext = hubContext;
    }

    public async Task Register(string serialId)
    {
        SerialIdConnectionId[serialId] = Context.ConnectionId;
    }

    public async Task Join(string serialId)
    {
        Context.Items[JoinedToKey] = SerialIdConnectionId[serialId];
    }

    public async Task RtcMessage(string message, string? connectionId)
    {
        if (connectionId is null)
        {
            connectionId = Context.Items[JoinedToKey] as string;
            await _hubContext.Clients.Client(connectionId).SendAsync("RtcMessage", message, Context.ConnectionId);
            return;
        }
        
        await _hubContext.Clients.Client(connectionId).SendAsync("RtcMessage", message);
    }
}