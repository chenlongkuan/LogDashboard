using System;
using System.Collections.Generic;
using LogDashboard;
using LogDashboard.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Log4Net.AspNetCore.Entities;

namespace UseLog4net
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddLogDashboard(opt =>
            {
                //opt.AddAuthorizationFilter(new LogDashboardBasicAuthFilter("admin", "admin"));
                opt.FileFieldDelimiterWithRegex = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            loggerFactory.AddLog4Net(new Log4NetProviderOptions
            {
                PropertyOverrides =
                    new List<NodeInfo>
                    {
                        new NodeInfo
                        {
                            XPath = "/log4net/appender/file[last()]",
                            Attributes = new Dictionary<string, string>
                                { { "value", $"{AppContext.BaseDirectory}logs/" } }
                        }
                    }
            });
            app.UseLogDashboard();
            app.Run(async (context) =>
            {
                //ThreadContext.Properties["identity"] = context.TraceIdentifier;
                var logger = app.ApplicationServices.GetService<ILogger<Startup>>();
                logger.LogInformation("来点日志");
                try
                {
                    throw new InvalidCastException("错误日志示例");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,ex.Message);
                }
                await context.Response.WriteAsync("Hello World!");
            });
        }
    }
}
