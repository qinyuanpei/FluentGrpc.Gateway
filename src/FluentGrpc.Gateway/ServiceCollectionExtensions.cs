using Google.Protobuf;
using Grpc.Core;
using FluentGrpc.Gateway.Swagger;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FluentGrpc.Gateway
{
    public static class ServiceCollectionExtensions
    {
        public static void AddGrpcGateway(this IServiceCollection services, IConfiguration configuration, Action<Microsoft.OpenApi.Models.OpenApiInfo> setupAction = null, string sectionName = "GrpcGateway")
        {
            var configSection = configuration.GetSection(sectionName);
            services.Configure<GrpcGatewayOptions>(configSection);

            // GrpcGatewayOptions
            var swaggerGenOptions = new GrpcGatewayOptions();
            configSection.Bind(swaggerGenOptions);

            var swaggerGenSetupAction = BuildDefaultSwaggerGenSetupAction(swaggerGenOptions, setupAction);
            services.AddSwaggerGen(swaggerGenSetupAction);

            // Replace ISwaggerProvider
            services.Replace(new ServiceDescriptor(
                typeof(ISwaggerProvider),
                typeof(GrpcSwaggerProvider),
                ServiceLifetime.Transient
            ));

            // Replace IApiDescriptionGroupCollectionProvider
            services.Replace(new ServiceDescriptor(
                typeof(IApiDescriptionGroupCollectionProvider),
                typeof(GrpcApiDescriptionsProvider),
                ServiceLifetime.Transient
            ));

            // GrpcDataContractResolver
            services.AddTransient<GrpcDataContractResolver>();

            // GrpcSwaggerSchemaGenerator
            services.AddTransient<GrpcSwaggerSchemaGenerator>();

            // Configure GrpcClients
            services.ConfigureGrpcClients(swaggerGenOptions);

            // AllowSynchronousIO
            services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true);
            services.Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
        }

        public static void AddGrpcGateway(this IServiceCollection services, string baseUrl, string urlPrefix = null, Action<Microsoft.OpenApi.Models.OpenApiInfo> setupAction = null)
        {
            var assembly = Assembly.GetEntryAssembly();

            // GrpcGatewayOptions
            var swaggerGenOptions = new GrpcGatewayOptions();
            swaggerGenOptions.BaseUrl = baseUrl;
            if (!string.IsNullOrEmpty(urlPrefix))
                swaggerGenOptions.UrlPrefix = urlPrefix;
            swaggerGenOptions.UpstreamInfos.Add(new UpstreamInfo(baseUrl, assembly));

            services.ConfigureAll<GrpcGatewayOptions>(swaggerGenOptions =>
            {
                swaggerGenOptions.BaseUrl = baseUrl;
                if (!string.IsNullOrEmpty(urlPrefix))
                    swaggerGenOptions.UrlPrefix = urlPrefix;
                swaggerGenOptions.UpstreamInfos.Add(new UpstreamInfo(baseUrl, assembly));
            });

            var swaggerGenSetupAction = BuildDefaultSwaggerGenSetupAction(swaggerGenOptions, setupAction);
            services.AddSwaggerGen(swaggerGenSetupAction);

            // Replace ISwaggerProvider
            services.Replace(new ServiceDescriptor(
                typeof(ISwaggerProvider),
                typeof(GrpcSwaggerProvider),
                ServiceLifetime.Transient
            ));

            // Replace IApiDescriptionGroupCollectionProvider
            services.Replace(new ServiceDescriptor(
                typeof(IApiDescriptionGroupCollectionProvider),
                typeof(GrpcApiDescriptionsProvider),
                ServiceLifetime.Transient
            ));

            // GrpcDataContractResolver
            services.AddTransient<GrpcDataContractResolver>();

            // GrpcSwaggerSchemaGenerator
            services.AddTransient<GrpcSwaggerSchemaGenerator>();

            // Configure GrpcClients
            services.ConfigureGrpcClients(swaggerGenOptions);

            // AllowSynchronousIO
            services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true);
            services.Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
        }

        public static void ConfigSwaggerGen(this IServiceCollection services, IConfiguration configuration, Action<SwaggerGenOptions> setupAction, string sectionName = "GrpcGateway")
        {
            // User Defined SwaggerGenOptions
            services.Configure<SwaggerGenOptions>(setupAction);

            //// Default SwaggerGenOptions
            //var configSection = configuration.GetSection(sectionName);
            //services.Configure<GrpcGatewayOptions>(configSection);

            //var swaggerGenOptions = new GrpcGatewayOptions();
            //configSection.Bind(swaggerGenOptions);

            //var defaultSetupAction = BuildDefaultSwaggerGenSetupAction(swaggerGenOptions);
            //services.Configure<SwaggerGenOptions>(defaultSetupAction);
        }

        public static void UseGrpcGateway(this IApplicationBuilder app)
        {
            var swaggerGenOptions = app.ApplicationServices.GetRequiredService<IOptions<GrpcGatewayOptions>>();
            var assemblies = swaggerGenOptions.Value.UpstreamInfos.Any() ?
                swaggerGenOptions.Value.GetAssemblies().ToArray() : new Assembly[] { };

            // Configure Swagger
            app.ConfigureSwagger(assemblies);

            // Configure Grpc Endpoints
            app.ConfigureGrpcEndpoints(assemblies);
        }

        private static Action<SwaggerGenOptions> BuildDefaultSwaggerGenSetupAction(GrpcGatewayOptions swaggerGenOptions, Action<Microsoft.OpenApi.Models.OpenApiInfo> setupAction = null)
        {
            Action<SwaggerGenOptions> swaggerDocSetupAction = options =>
            {
                if (swaggerGenOptions.UpstreamInfos != null && swaggerGenOptions.UpstreamInfos.Any())
                {
                    foreach (var assembly in swaggerGenOptions.GetAssemblies())
                    {
                        var assemblyName = assembly.GetName().Name;

                        var openApiInfo = new Microsoft.OpenApi.Models.OpenApiInfo();

                        if (setupAction == null)
                            setupAction = BuildDefaultOpenApiInfoSetupAction(assemblyName);

                        setupAction(openApiInfo);
                        openApiInfo.Title = assemblyName;
                        if (!options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey(assemblyName))
                            options.SwaggerDoc(assemblyName, openApiInfo);
                        if (!options.SwaggerGeneratorOptions.Servers.Any())
                            options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer() { Url = swaggerGenOptions.BaseUrl });

                    }
                }
            };

            return swaggerDocSetupAction;
        }

        private static Action<Microsoft.OpenApi.Models.OpenApiInfo> BuildDefaultOpenApiInfoSetupAction(string assemblyName)
        {
            Action<Microsoft.OpenApi.Models.OpenApiInfo> setupAction = apiInfo =>
            {
                apiInfo.Title = assemblyName;
                apiInfo.Version = "v1";
                apiInfo.License = new Microsoft.OpenApi.Models.OpenApiLicense()
                {
                    Name = "The MIT License",
                    Url = new Uri("https://mit-license.org/")
                };
                apiInfo.Contact = new Microsoft.OpenApi.Models.OpenApiContact()
                {
                    Name = "飞鸿踏雪",
                    Email = "qinyuanpei@163.com",
                    Url = new Uri("https://blog.yuanpei.me"),
                };
            };

            return setupAction;
        }

        private static void ConfigureSwagger(this IApplicationBuilder app, IEnumerable<Assembly> assemblies)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                foreach (var assembly in assemblies)
                {
                    var assemblyName = assembly.GetName().Name;
                    c.SwaggerEndpoint($"/swagger/{assemblyName}/swagger.json", $"{assemblyName}");
                }
            });
        }

        private static void ConfigureGrpcEndpoints(this IApplicationBuilder app, Assembly[] assemblies)
        {
            var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("GrpcGateway");

            var swaggerGenOptions = app.ApplicationServices.GetRequiredService<IOptions<GrpcGatewayOptions>>();
            var urlPrefix = swaggerGenOptions.Value?.UrlPrefix ?? string.Empty;

            var clientTypes = AggregateGrpcClientTypes(assemblies);

            foreach (var clientType in clientTypes)
            {
                // Protobuf ServiceInfo
                var serviceType = assemblies.SelectMany(x => x.DefinedTypes).FirstOrDefault(x => x.DeclaredNestedTypes.Contains(clientType));
                var serviceDescriptor = serviceType.GetDeclaredProperty("Descriptor").GetValue(null) as Google.Protobuf.Reflection.ServiceDescriptor;

                foreach (var method in clientType.GetMethods().Where(x => x.Name.EndsWith("Async") && x.GetParameters().Length == 4))
                {
                    var methodName = method.Name.Replace("Async", "");
                    var grpcRoute = $"{serviceDescriptor.FullName}/{methodName}";
                    if (!string.IsNullOrEmpty(urlPrefix))
                        grpcRoute = $"{urlPrefix}/{grpcRoute}";
                    logger.LogInformation($"Generate gRPC Gateway: {grpcRoute}");
                    app.UseEndpoints(endpoints => endpoints.MapPost($"{grpcRoute}", async context =>
                    {
                        using (var streamReader = new StreamReader(context.Request.Body))
                        {
                            var client = app.ApplicationServices.GetService(clientType);

                            var payload = await streamReader.ReadToEndAsync();
                            var requestType = method.GetParameters()[0].ParameterType;
                            var messageParser = CreateMessageParser(requestType);
                            dynamic request = CallParseJson(messageParser, payload);

                            dynamic reply = CallRpcMethod(client, method, request);

                            var response = JsonConvert.SerializeObject(reply.ResponseAsync.Result);

                            context.Response.Headers.Add("X-Grpc-Service", $"{serviceDescriptor.FullName}");
                            context.Response.Headers.Add("X-Grpc-Method", $"{methodName}");
                            context.Response.Headers.Add("X-Grpc-Client", $"{client.GetType().FullName}");

                            context.Response.StatusCode = 200;
                            context.Response.ContentType = "application/json";
                            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response));
                        }
                    }));
                }
            }
        }

        private static void ConfigureGrpcClients(this IServiceCollection services, GrpcGatewayOptions swaggerGenOptions)
        {
            if (swaggerGenOptions.UpstreamInfos != null && swaggerGenOptions.UpstreamInfos.Any())
            {
                var upstreamInfos = swaggerGenOptions.UpstreamInfos;
                foreach (var upstreamInfo in upstreamInfos)
                {
                    var assembly = upstreamInfo.GetAssembly();
                    var assemblyName = assembly.GetName().Name;

                    Action<GrpcClientFactoryOptions> clientSetupAction = options =>
                    {
                        options.Address = new Uri(upstreamInfo.BaseUrl);
                    };

                    var clientTypes = AggregateGrpcClientTypes(assembly);
                    foreach (var clientType in clientTypes)
                    {
                        CallAddGrpcClient(clientType, services, clientSetupAction);
                    }
                }
            }

        }

        private static IEnumerable<Type> AggregateGrpcClientTypes(params Assembly[] assemblies)
        {
            return assemblies
                .SelectMany(x => x.DefinedTypes)
                .Where(x => x.BaseType != null && x.BaseType.IsGenericType && x.BaseType.GetGenericTypeDefinition() == typeof(ClientBase<>))
                .ToList();
        }

        private static dynamic CreateMessageParser(Type type)
        {
            // () => new HelloRequest()
            var constructor = type.GetConstructor(Type.EmptyTypes);
            var messageNew = Expression.New(constructor);
            var messageLambda = Expression.Lambda(messageNew, null);

            // var parser = new MessageParser(() => new HelloRequest()); 
            var factoryType = typeof(Func<>).MakeGenericType(type);
            var parserType = typeof(MessageParser<>).MakeGenericType(type);
            constructor = parserType.GetConstructor(new Type[] { factoryType });
            var parserNew = Expression.New(constructor, messageLambda);
            var parserLambda = Expression.Lambda(parserNew, null);

            // return parser
            var parserFactory = parserLambda.Compile();
            return parserFactory.DynamicInvoke();
        }

        private static dynamic CallParseJson(object messageParser, string json)
        {
            // () => messageParser.ParseJson(json)
            var parserInstance = Expression.Constant(messageParser);
            var parseJsonMethod = Expression.Call(
                parserInstance,
                messageParser.GetType().GetMethod("ParseJson"),
                new List<Expression> { Expression.Constant(json) }
            );

            var lambdaExp = Expression.Lambda(parseJsonMethod, null);
            var callInvoker = lambdaExp.Compile();
            return callInvoker.DynamicInvoke();
        }

        private static dynamic CallRpcMethod(dynamic rpcClient, MethodInfo methodInfo, dynamic request)
        {
            // () => client.SayHelloAsync(request)
            var clientInstance = Expression.Constant(rpcClient, rpcClient.GetType());
            var parseJsonMethod = Expression.Call(
                clientInstance,
                methodInfo,
                new List<Expression> {
                    Expression.Constant(request, request.GetType()),
                    Expression.Constant(null, typeof(Metadata)),
                    Expression.Constant(null, typeof(Nullable<System.DateTime>)),
                    Expression.Constant(default(global::System.Threading.CancellationToken)),
                }
            );

            var lambdaExp = Expression.Lambda(parseJsonMethod, null);
            var callInvoker = lambdaExp.Compile();
            return callInvoker.DynamicInvoke();
        }

        private static void CallAddGrpcClient(Type type, IServiceCollection services, Action<GrpcClientFactoryOptions> configureClient)
        {
            var methodCall = Expression.Call(
                null,
                typeof(GrpcClientServiceExtensions).GetMethod("AddGrpcClient", new Type[] { typeof(IServiceCollection), typeof(Action<GrpcClientFactoryOptions>) }).MakeGenericMethod(type),
                new List<Expression> {
                    Expression.Constant(services, services.GetType()),
                    Expression.Constant(configureClient, configureClient.GetType()),
                }
            );

            var lambdaExp = Expression.Lambda(methodCall, null);
            lambdaExp.Compile().DynamicInvoke();
        }
    }
}
