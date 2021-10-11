using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    public class GrpcApiDescriptionsProvider : IApiDescriptionGroupCollectionProvider
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly GrpcDataContractResolver _resolver;
        private IOptions<GrpcGatewayOptions> _options;

        public GrpcApiDescriptionsProvider(
            GrpcDataContractResolver resolver,
            IOptions<GrpcGatewayOptions> options)
        {
            _resolver = resolver;
            _options = options;
            _assemblies = options.Value.UpstreamInfos.Any() ?
                options?.Value.GetAssemblies() : new List<Assembly>();
        }


        public ApiDescriptionGroupCollection ApiDescriptionGroups => BuildApiDescriptionGroups();

        private ApiDescriptionGroupCollection BuildApiDescriptionGroups()
        {
            var apiDescriptionGroups = new List<ApiDescriptionGroup>();

            foreach (var assembly in _assemblies)
            {
                var apiDescriptionGroup = BuildApiDescriptionGroup(assembly);
                apiDescriptionGroups.AddRange(apiDescriptionGroup);
            }

            return new ApiDescriptionGroupCollection(apiDescriptionGroups, 0);
        }

        private IEnumerable<ApiDescriptionGroup> BuildApiDescriptionGroup(Assembly assembly)
        {
            var apiDescriptions = new List<ApiDescription>();

            var assemblyName = assembly.GetName().Name;

            var clientTypes = assembly.DefinedTypes
                .Where(x => x.BaseType != null && x.BaseType.IsGenericType && x.BaseType.GetGenericTypeDefinition() == typeof(ClientBase<>));

            foreach (var clientType in clientTypes)
            {
                // Protobuf ServiceInfo
                var serviceType = assembly.DefinedTypes.FirstOrDefault(x => x.DeclaredNestedTypes.Contains(clientType));
                var serviceDescriptor = serviceType.GetDeclaredProperty("Descriptor").GetValue(null) as Google.Protobuf.Reflection.ServiceDescriptor;

                foreach (var method in clientType.GetMethods().Where(x => x.Name.EndsWith("Async") && x.GetParameters().Length == 4))
                {
                    // Protobuf MethodInfo
                    var methodName = method.Name.Replace("Async", "");
                    var methodDescriptor = serviceDescriptor.Methods.FirstOrDefault(x => x.Name == methodName);

                    // ApiDescription
                    var apiDescription = new ApiDescription();
                    apiDescription.GroupName = serviceDescriptor.FullName.Split(new char[] { '.' })[0];
                    apiDescription.HttpMethod = "POST";
                    apiDescription.RelativePath = $"/{serviceDescriptor.FullName}/{methodName}";
                    if (!string.IsNullOrEmpty(_options.Value?.UrlPrefix))
                        apiDescription.RelativePath = $"/{_options.Value.UrlPrefix}{apiDescription.RelativePath}";
                    apiDescription.Properties.Add("ServiceDescriptor", serviceDescriptor);
                    apiDescription.Properties.Add("ServiceAssembly", assemblyName);
                    apiDescription.ActionDescriptor = BuildActionDescriptor(method, methodDescriptor);

                    apiDescriptions.Add(apiDescription);
                }
            }

            return apiDescriptions.GroupBy(x => x.GroupName).Select(x => new ApiDescriptionGroup(x.Key, x.ToList()));
        }

        private ActionDescriptor BuildActionDescriptor(MethodInfo methodInfo, MethodDescriptor methodDescriptor)
        {
            var actionDescriptor = new ActionDescriptor();
            actionDescriptor.ActionConstraints = new List<IActionConstraintMetadata>();
            actionDescriptor.ActionConstraints.Add(new HttpMethodActionConstraint(new string[] { "POST" }));
            actionDescriptor.DisplayName = methodInfo.Name.Replace("Async", "");
            actionDescriptor.Properties.Add("MethodDescriptor", methodDescriptor);
            actionDescriptor.Parameters = BuildActionParameterDescriptors(methodInfo).ToList();
            return actionDescriptor;
        }

        private IEnumerable<ParameterDescriptor> BuildActionParameterDescriptors(MethodInfo methodInfo)
        {
            var requestType = methodInfo.GetParameters()[0].ParameterType;
            var requestInfo = Activator.CreateInstance(requestType) as IMessage;
            foreach (var field in requestInfo.Descriptor.Fields.InFieldNumberOrder())
                yield return new ParameterDescriptor() { Name = field.Name, ParameterType = _resolver.ResolveFieldType(field) };
        }
    }
}
