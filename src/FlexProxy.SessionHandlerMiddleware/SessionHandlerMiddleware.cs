﻿using FlexProxy.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace FlexProxy.SessionHandlerMiddleware
{
    public class SessionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private ILogger _logger;
        private IOptions<SessionHandlerOptions> _options;

        public SessionHandlerMiddleware(RequestDelegate next, 
            ILoggerFactory loggerFactory, 
            IOptions<SessionHandlerOptions> options)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<SessionHandlerMiddleware>();
            _options = options;
        }

        public async Task Invoke(HttpContext context)
        {
            await _next.Invoke(context);

            //TODO: Working in Web Listener but not in Kestrel
            //if (context.Request.Cookies[_options.Value.EventSessionCookieName] == null)
            //{
            //    var newSessionId = Guid.NewGuid();
            //    context.Response.Headers.Append(HttpHeaders.SetCookie, $"{_options.Value.EventSessionCookieName}={newSessionId}");
            //}
        }
    }
}
