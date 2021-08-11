using Google.Api;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    public class GrpcSwaggerProvider : ISwaggerProvider
    {
        private readonly ISchemaGenerator _schemaGenerator;
        private readonly SwaggerGeneratorOptions _options;
        private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionsProvider;
        private readonly GrpcSwaggerSchemaGenerator _swaggerSchemaGenerator;

        public GrpcSwaggerProvider(
            SwaggerGeneratorOptions options, 
            ISchemaGenerator schemaGenerator, 
            IApiDescriptionGroupCollectionProvider apiDescriptionsProvider,
            GrpcSwaggerSchemaGenerator swaggerSchemaGenerator
            )
        {
            _options = options;
            _schemaGenerator = schemaGenerator;
            _apiDescriptionsProvider = apiDescriptionsProvider;
            _swaggerSchemaGenerator = swaggerSchemaGenerator;
        }

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null)
        {
            if (!_options.SwaggerDocs.TryGetValue(documentName, out OpenApiInfo info))
                throw new UnknownSwaggerDocument(documentName, _options.SwaggerDocs.Select(d => d.Key));

            var schemaRepository = new SchemaRepository(documentName);

            // Swagger Document
            var swaggerDoc = new OpenApiDocument
            {
                Info = info,
                Servers = BuildOpenApiServers(host, basePath),
                Paths = new OpenApiPaths() { },
                Components = new OpenApiComponents
                {
                    Schemas = schemaRepository.Schemas,
                    SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>(_options.SecuritySchemes)
                },
                SecurityRequirements = new List<OpenApiSecurityRequirement>(_options.SecurityRequirements)
            };

            // Swagger Filters
            var apiDescriptions = _apiDescriptionsProvider.GetApiDescriptions().Where(x => x.Properties["ServiceAssembly"]?.ToString() == documentName);
            var filterContext = new DocumentFilterContext(apiDescriptions, _schemaGenerator, schemaRepository);
            foreach (var filter in _options.DocumentFilters)
            {
                filter.Apply(swaggerDoc, filterContext);
            }

            // Swagger Schemas
            swaggerDoc.Components.Schemas = _swaggerSchemaGenerator.BuildSchemas(apiDescriptions);
            var apiDescriptionsGroups = _apiDescriptionsProvider.ApiDescriptionGroups.Items.Where(x => x.Items.Any(y => y.Properties["ServiceAssembly"]?.ToString() == documentName));
            swaggerDoc.Paths = _swaggerSchemaGenerator.BuildOpenApiPaths(apiDescriptionsGroups);

            return swaggerDoc;
        }


        private IList<OpenApiServer> BuildOpenApiServers(string host, string basePath)
        {
            if (_options.Servers.Any())
            {
                return new List<OpenApiServer>(_options.Servers);
            }

            return (host == null && basePath == null)
                ? new List<OpenApiServer>()
                : new List<OpenApiServer> { new OpenApiServer { Url = $"{host}{basePath}" } };
        }
    }

    public class MessageDescriptorCompare : IEqualityComparer<MessageDescriptor>
    {
        public bool Equals(MessageDescriptor x, MessageDescriptor y)
        {
            return string.Equals(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetHashCode(MessageDescriptor obj)
        {
            return obj.GetHashCode();
        }
    }
}
