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
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace serverless_signalr_poc
{
    public class PocHub : ServerlessHub
    {
        private static readonly SymmetricSecurityKey Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("401b09eab3c013d4ca54922bb802bec8fd5318192b0a75f201d8b3727429090fb337591abd3e44453b954555b7a0812e1081c39b740293f765eae731f5a65ed1"));
        private static readonly SymmetricSecurityKey EncryptKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ProEMLh5e_qnzdNU"));
        private static readonly JwtSecurityTokenHandler Handler = new JwtSecurityTokenHandler();

        [FunctionName("negotiate")]
        public IActionResult Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req, ILogger logger)
        {
            var auth = req.Headers["X-Token"].ToString();
            SecurityToken validatedToken;
            if (auth.IndexOf("Bearer") < 0) return new UnauthorizedResult();
            try
            {
                Handler.ValidateToken(auth.Replace("Bearer ", string.Empty), new TokenValidationParameters
                {
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = Key,
                    TokenDecryptionKey = EncryptKey,
                    ValidateAudience = true,
                    ValidAudience = "testAud",
                    ValidateIssuer = true,
                    ValidIssuer = "testIss"
                }, out validatedToken);
            }  catch (Exception)
            {
                return new UnauthorizedResult();
            }
            var validatedTokenAsJwt = validatedToken as JwtSecurityToken;
            var x = Negotiate(req.Headers["x-ms-signalr-user-id"], validatedTokenAsJwt.Claims.ToList(), TimeSpan.FromTicks(validatedTokenAsJwt.ValidTo.Ticks));
            return new OkObjectResult(x);
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

        [FunctionName(nameof(GenerateToken))]
        public IActionResult GenerateToken([HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "token")] HttpRequest req, ILogger logger)
        {
            var creds = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256Signature);
            var eCreds = new EncryptingCredentials(EncryptKey, SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);
            var token = Handler.CreateJwtSecurityToken("testIss", "testAud", null, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow, creds, eCreds);
            return new OkObjectResult(new JObject()
            {
                { "token", $"Bearer {Handler.WriteToken(token)}" }
            });
        }
    }
}
