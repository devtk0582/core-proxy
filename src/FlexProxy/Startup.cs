﻿using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FlexProxy.ContentModificationMiddleware;
using FlexProxy.ContentModificationMiddleware.ContentAbstraction;
using FlexProxy.Core;
using FlexProxy.Core.Models;
using FlexProxy.Core.Services;
using FlexProxy.ExceptionHandlerMiddleware;
using FlexProxy.HealthCheckMiddleware;
using FlexProxy.RequestTracerMiddleware;
using FlexProxy.RobotsMiddleware;
using FlexProxy.SessionHandlerMiddleware;
using FlexProxy.WebProxyMiddleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlexProxy
{
    public class Startup
    {
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private IServiceProvider _serviceProvider;
        private string[] _args;

        static public IConfigurationRoot Configuration { get; set; }

        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            loggerFactory.AddConsole();
            _logger = loggerFactory.CreateLogger<Startup>();
            _args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            _logger.LogDebug($"Startup args: {string.Join(" ", _args)}");

            var builder = new ConfigurationBuilder()
                .AddCommandLine(_args)
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile($"appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true);

            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureOptions(services);

            services.AddSingleton<IRequestTraceLoggerService, RequestTraceLoggerService>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddResponseCompression();
            services.AddContentModification();
            services.AddWebProxy();
        }

        internal void ConfigureOptions(IServiceCollection services)
        {
            services.Configure<HostMappingOptions>(options =>
            {
                options.ServingHost = Configuration["ServingHost"];
                options.ServingScheme = Configuration["ServingScheme"];
                options.DownstreamHost = Configuration["DownstreamHost"];
                options.DownstreamScheme = Configuration["DownstreamScheme"];
            });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            services.Configure<ResponseCompressionOptions>(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "text/javascript" });
            });

            services.Configure<ContentModificationOptions>(options =>
            {

                options.ContentProviders.Add<HtmlDocumentAbstraction>(new string[] { "text/html" });
                options.ContentProviders.Add<FormAbstraction>(new string[] { "multipart/form-data", "application/x-www-form-urlencoded" });
                options.ContentProviders.Add<JavascriptAbstraction>(new string[] { "text/javascript", "​text/x-javascript", "application/javascript", "application/x-javascript", "text/ecmascript", "application/ecmascript", "text/jscript" });
                options.ContentProviders.Add<JsonAbstraction>(new string[] { "application/json" });
                options.LogApiOptions.LogApiBaseUrl = Configuration["LoggingApiBaseUrl"];
                options.LogApiOptions.LogLevels = Configuration["LogLevels"];
            });

            services.Configure<WebProxyOptions>(options =>
            {
                options.InternalCookies = new List<string> { Configuration["EventSessionCookieName"] };
            });

            services.Configure<SessionHandlerOptions>(options =>
            {
                options.EventSessionCookieName = Configuration["EventSessionCookieName"];
            });

            services.Configure<HealthCheckOptions>(options =>
            {
                options.Localhost = IPAddress.Parse("127.0.0.1");
                options.MaxResponseTimeInSeconds = int.Parse(Configuration["MaxResponseTimeInSeconds"]);
            });

            services.Configure<RequestTraceLoggerServiceOptions>(options =>
            {
                options.LoggerConfigName = Constants.REQUEST_TRACELOGGER_NAME;
                options.Interval = Constants.REQUEST_TRACELOGGER_INTERVAL;
                options.ProcessName = Configuration["ServingHost"];
            });

            //TODO: move modifiers to external source like api or config files
            services.Configure<ModifierOptions>(options =>
            {
                options.Modifiers = new List<JavascriptModifier>
                    {
                        new JavascriptModifier
                        {
                            Priority = 1,
                            RequestPhase = RequestPhase.Response,
                            TargetContentType = "text/html",
                            ModificationFunction = GetSampleModificationFunction()
                        }
                    };
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseHealthCheck();
            app.UseRequestTracer();
            app.UseCustomExceptionHandler();
            app.UseRobots();
            app.UseResponseCompression();
            app.UseContentModification();
            app.UseSessionHandler();
            app.UseWebProxy();
        }

        private string GetSampleModificationFunction()
        {
            return @"var form = document.GetElementByXPath(""/html/body/footer"");

if (form) {
    var divMyButton = document.AppendElementToPath(""/html/body/footer"", ""div"");
    divMyButton.SetAttributeValue(""Id"", ""divMyButton"");

    var myBtn = document.AppendElementToPath(""/html/body/footer/div[@id='divMyButton']"", ""a"");
    myBtn.SetAttributeValue(""href"", ""https://github.com/devtk0582"");
    myBtn.InnerHtml = ""Go to DevTk0582"";

    document.Save();
}";
        }
    }
}
