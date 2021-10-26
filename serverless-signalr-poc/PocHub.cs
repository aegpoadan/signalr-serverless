using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;

namespace serverless_signalr_poc
{
    public class PocHub : ServerlessHub
    {
        [FunctionName("negotiate")]
        public SignalRConnectionInfo Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req, ILogger logger)
        {
            var x = Negotiate(req.Headers["x-ms-signalr-user-id"]);
            return x;
        }

        [FunctionName(nameof(OnConnected))]
        public async Task OnConnected([SignalRTrigger] InvocationContext invocationContext, ILogger logger)
        {
            invocationContext.Headers.TryGetValue("Authorization", out var auth);
            await Clients.All.SendAsync("connected", invocationContext.ConnectionId);
        }

        [FunctionName(nameof(JoinUserToGroup))]
        public async Task JoinUserToGroup([SignalRTrigger] InvocationContext invocationContext, string userName, string groupName, ILogger logger)
        {
            await UserGroups.AddToGroupAsync(userName, groupName);
            var userInGroup = await UserGroups.IsUserInGroup(userName, groupName);
            await Clients.Group(groupName).SendAsync("userAdded", new ReturnObj() { 
                TriggeringUser = userName
            });
        }

        [FunctionName(nameof(JoinUserError))]
        public async Task JoinUserError([SignalRTrigger] InvocationContext invocationContext, string userName, string groupName, ILogger logger)
        {
            await UserGroups.RemoveFromGroupAsync(userName, groupName);
            var userInGroup = await UserGroups.IsUserInGroup(userName, groupName);
            throw new Exception();
        }
    }
}
