using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System;
using System.Collections.Generic;
using System.Text;
using Grpc.Reflection.V1Alpha;
using Grpc.Net.Client;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.Options;
using FluentGrpc.Gateway.Swagger;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Google.Protobuf;

namespace FluentGrpc.Gateway.ApiDescriptors
{
    public class ServerReflectionApiDescriptionProvider : IApiDescriptionGroupCollectionProvider
    {
        private IOptions<GrpcGatewayOptions> _options;

        private readonly GrpcDataContractResolver _resolver;

        private readonly ServerReflection.ServerReflectionClient _serverReflectionClient;

        private const string GRPC_SERVER_REFLECTION = "grpc.reflection.v1alpha.ServerReflection";

        public ServerReflectionApiDescriptionProvider(
            IOptions<GrpcGatewayOptions> options,
            GrpcDataContractResolver resolver,
            ServerReflection.ServerReflectionClient client
        )
        {
            _options = options;
            _resolver = resolver;
            _serverReflectionClient = client;
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups => BuildApiDescriptionGroups().GetAwaiter().GetResult();

        private async Task<ApiDescriptionGroupCollection> BuildApiDescriptionGroups()
        {
            var cts = new CancellationTokenSource();
            var deadline = DateTime.UtcNow.AddSeconds(30);

            var serviceNames = await ResolveListServices(deadline, cts.Token, serviceName => serviceName != GRPC_SERVER_REFLECTION);

            var fileProtosTasks = serviceNames.Select(serviceName => ResolveFileProtos(serviceName, deadline, cts.Token)).ToArray();
            var fileProtos = (await Task.WhenAll(fileProtosTasks)).SelectMany(proto => proto).ToList();

            var fileDescriptors = ResolveFileDescriptors(fileProtos);

            var apiDescriptionGroups = BuildApiDescriptionGroup(fileDescriptors);

            return new ApiDescriptionGroupCollection(apiDescriptionGroups, 0);
        }

        private async Task<List<string>> ResolveListServices(DateTime deadline, CancellationToken cancellationToken, Predicate<string> filter)
        {
            if (filter == null)
                filter = serviceName => true;

            var callResult = _serverReflectionClient.ServerReflectionInfo(deadline: deadline, cancellationToken: cancellationToken);

            var resolveServiceListTask = Task.Run(async () =>
            {
                var serviceNames = new List<string>();

                while (await callResult.ResponseStream.MoveNext(cancellationToken))
                {
                    foreach (var service in callResult.ResponseStream.Current.ListServicesResponse.Service)
                    {
                        if (filter(service.Name))
                        {
                            serviceNames.Add(service.Name);
                        }
                    }
                }

                return serviceNames;
            });

            var request = new ServerReflectionRequest() { ListServices = "" };
            await callResult.RequestStream.WriteAsync(request);
            await callResult.RequestStream.CompleteAsync();

            return await resolveServiceListTask;
        }

        private async Task<List<BuffedFileProto>> ResolveFileProtos(string serviceName, DateTime deadline, CancellationToken cancellationToken)
        {
            var callResult = _serverReflectionClient.ServerReflectionInfo(deadline: deadline, cancellationToken: cancellationToken);

            var resolveFileDescriptorTask = Task.Run(async () =>
            {
                var buffedFileProtos = new List<BuffedFileProto>();

                while (await callResult.ResponseStream.MoveNext(cancellationToken))
                {
                    var buffers = callResult.ResponseStream.Current.FileDescriptorResponse.FileDescriptorProto.ToList();
                    buffers.ForEach(buffer =>
                    {
                        var proto = FileDescriptorProto.Parser.ParseFrom(buffer.ToByteArray());
                        buffedFileProtos.Add(new BuffedFileProto() { Buffer = buffer, Proto = proto });
                    });
                }

                return buffedFileProtos;
            });

            var request = new ServerReflectionRequest() { FileContainingSymbol = serviceName };
            await callResult.RequestStream.WriteAsync(request);
            await callResult.RequestStream.CompleteAsync();

            return await resolveFileDescriptorTask;
        }

        private List<FileDescriptor> ResolveFileDescriptors(List<BuffedFileProto> buffedFileProtos)
        {
            var sortedProtos = new List<ByteString>();
            var loadedProtos = buffedFileProtos.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.ToList()[0]);
            var resolvedProtos = new HashSet<string>();

            while (loadedProtos.Count() > 0)
            {
                var buffedFileProto = loadedProtos.Values.FirstOrDefault(x => x.Proto.Dependency.All(dependency => resolvedProtos.Contains(dependency)));
                if (buffedFileProto != null)
                {
                    resolvedProtos.Add(buffedFileProto.Name);
                    loadedProtos.Remove(buffedFileProto.Name);
                    sortedProtos.Add(buffedFileProto.Buffer);
                }
            }

            return FileDescriptor.BuildFromByteStrings(sortedProtos).ToList();
        }

        private List<ApiDescriptionGroup> BuildApiDescriptionGroup(IEnumerable<FileDescriptor> fileDescriptors)
        {
            var apiDescriptions = new List<ApiDescription>();

            var messageDescriptors = fileDescriptors.SelectMany(file => file.MessageTypes);

            foreach (var fileDescriptor in fileDescriptors)
            {
                foreach (var serviceDescriptor in fileDescriptor.Services)
                {
                    foreach (var methodDescriptor in serviceDescriptor.Methods)
                    {
                        var apiDescription = new ApiDescription();
                        apiDescription.GroupName = $"{fileDescriptor.Package}.{serviceDescriptor.Name}";
                        apiDescription.HttpMethod = "POST";
                        apiDescription.RelativePath = $"/{fileDescriptor.Package}.{serviceDescriptor.Name}/{methodDescriptor.Name}";
                        if (!string.IsNullOrEmpty(_options.Value?.UrlPrefix))
                            apiDescription.RelativePath = $"/{_options.Value.UrlPrefix}{apiDescription.RelativePath}";
                        apiDescription.ActionDescriptor = BuildActionDescriptor(methodDescriptor);
                        apiDescriptions.Add(apiDescription);
                    }

                }
            }

            return apiDescriptions.GroupBy(x => x.GroupName).Select(x => new ApiDescriptionGroup(x.Key, x.ToList())).ToList();
        }

        private ActionDescriptor BuildActionDescriptor(MethodDescriptor methodDescriptor)
        {
            var actionDescriptor = new ActionDescriptor();
            actionDescriptor.ActionConstraints = new List<IActionConstraintMetadata>();
            actionDescriptor.ActionConstraints.Add(new HttpMethodActionConstraint(new string[] { "POST" }));
            actionDescriptor.DisplayName = methodDescriptor.Name;
            actionDescriptor.Properties.Add("MethodDescriptor", methodDescriptor);
            actionDescriptor.Parameters = BuildActionParameterDescriptors(methodDescriptor).ToList();
            return actionDescriptor;
        }

        private IEnumerable<ParameterDescriptor> BuildActionParameterDescriptors(MethodDescriptor methodDescriptor)
        {
            var requestType = methodDescriptor.InputType;
            foreach (var field in requestType.Fields.InFieldNumberOrder())
                yield return new ParameterDescriptor() { Name = field.Name, ParameterType = _resolver.ResolveFieldType(field) };
        }
    }
}
