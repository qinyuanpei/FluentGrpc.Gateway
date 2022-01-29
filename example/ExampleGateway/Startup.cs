using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentGrpc.Gateway;
using System.Reflection;
using Swashbuckle.AspNetCore.Filters;
using Microsoft.OpenApi.Models;
using FluentGrpc.Gateway.ApiDescriptors;
using Grpc.Reflection.V1Alpha;

namespace ExampleGateway
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddRazorPages();
            services.AddGrpcClient<ServerReflection.ServerReflectionClient>(option =>
            {
                option.Address = new Uri("http://localhost:8000");
            });
            services.AddControllers();
            services.AddTransient<ServerReflectionApiDescriptionProvider>();
            services.AddGrpcGateway(Configuration);

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime)
        {
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                var provider = app.ApplicationServices.GetService<ServerReflectionApiDescriptionProvider>();
                var apiDescriptions = provider.ApiDescriptionGroups.Items;
            });
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
            });

            app.UseGrpcGateway();
        }
    }
}
