using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs
{
    // Broadcasts live insole/wearable readings to whoever is watching an athlete:
    // the athlete's own dashboard/app, plus a coach/doctor who explicitly joins that
    // athlete's group (e.g. viewing a live session).
    [Authorize]
    public class VitalsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();

            // Athletes auto-join their own group so their dashboards get live readings
            // without an extra round trip.
            await Groups.AddToGroupAsync(Context.ConnectionId, $"athlete-{userId}");

            await base.OnConnectedAsync();
        }

        // Coach/doctor/scout dashboards call this to watch a specific athlete's feed.
        public async Task JoinAthleteGroup(string athleteId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"athlete-{athleteId}");
        }

        public async Task LeaveAthleteGroup(string athleteId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"athlete-{athleteId}");
        }

        private string GetUserId() =>
            Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("Not authenticated.");
    }
}
