﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using CDR.DataHolder.Resource.API.Business;

namespace CDR.DataHolder.Resource.API.Middleware
{
    public class ResourceAuthoriseErrorHandlingMiddleware : IMiddleware
    {
        private readonly ILogger<ResourceAuthoriseErrorHandlingMiddleware> _logger;

        public ResourceAuthoriseErrorHandlingMiddleware(
            ILogger<ResourceAuthoriseErrorHandlingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
        {
            await next.Invoke(httpContext);

            try
            {
                if (httpContext.Request.Path.ToString().EndsWith(Constants.ResourceEndPoints.GetAccounts) && httpContext.Response.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    try
                    {
                        httpContext.Request.Headers.TryGetValue("Authorization", out StringValues authHeader);
                        var accessToken = authHeader.ToString().Replace("Bearer ", "");
                        var jwt = accessToken;

                        // Try to get the token. Will fail if the token is invalid
                        var handler = new JwtSecurityTokenHandler();
                        var token = handler.ReadJwtToken(jwt);
                    }
                    catch
                    {
                        // Token creation failed. Set error message to invalid_token
                        await SetUnauthorisedErrorResponseAsync(httpContext, Constants.UnauthorisedErrors.InvalidToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing the Resource Middleware {ExceptionMessage} {StackTrace}", ex.Message, ex.StackTrace);
            }
        }

        private async Task SetUnauthorisedErrorResponseAsync(HttpContext httpContext, string error)
        {
            // Replace the response body with the custom error message
            var originBody = httpContext.Response.Body;

            var memStream = new MemoryStream();
            httpContext.Response.Body = memStream;

            memStream.Position = 0;
            var responseBody = new StreamReader(memStream).ReadToEnd();

            // Get the custom error message
            responseBody = Constants.UnauthorisedErrors.ErrorMessage.Replace(Constants.UnauthorisedErrors.ErrorMessageDetailReplace, error);

            var memoryStreamModified = new MemoryStream();
            var sw = new StreamWriter(memoryStreamModified);
            sw.Write(responseBody);
            sw.Flush();
            memoryStreamModified.Position = 0;

            await memoryStreamModified.CopyToAsync(originBody).ConfigureAwait(false);
        }
    }
}
