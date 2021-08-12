using Google.Api;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    public class GrpcSwaggerSchemaGenerator
    {
        private readonly GrpcDataContractResolver _resolver;
        public GrpcSwaggerSchemaGenerator(GrpcDataContractResolver resolver)
        {
            _resolver = resolver;
        }

        public IDictionary<string, OpenApiSchema> BuildSchemas(IEnumerable<ApiDescription> apiDescriptions)
        {
            // Method Descriptor
            var methodDescriptor = apiDescriptions.Select(
                x => x.ActionDescriptor.Properties["MethodDescriptor"] as MethodDescriptor
            );

            // Message Descriptor
            var inputTypes = methodDescriptor.Select(x => x.InputType);
            var outputTypes = methodDescriptor.Select(x => x.OutputType);
            var descriptors = inputTypes.Concat(outputTypes).ToList(); ;

            return CreateSchemas(descriptors);
        }


        public OpenApiPaths BuildOpenApiPaths(IEnumerable<ApiDescriptionGroup> apiDescriptionGroups)
        {
            var opanApiPaths = new OpenApiPaths();

            foreach (var apiDescriptionGroup in apiDescriptionGroups)
                foreach (var apiDescription in apiDescriptionGroup.Items)
                    opanApiPaths.Add(apiDescription.RelativePath, BuildOpenApiPathItem(apiDescription));

            return opanApiPaths;
        }

        private OpenApiPathItem BuildOpenApiPathItem(ApiDescription apiDescription)
        {
            var methodDescriptor = apiDescription.ActionDescriptor.Properties["MethodDescriptor"] as MethodDescriptor;
            var apiItem = new OpenApiPathItem();
            var operation = new OpenApiOperation();
            operation.Tags = new List<OpenApiTag> { new OpenApiTag() { Name = methodDescriptor.Service.FullName, Description = "" } };
            operation.Responses.Add("200", CreateResponseBody(methodDescriptor));
            operation.RequestBody = CreateRequestBody(methodDescriptor);
            operation.Description = apiDescription.ActionDescriptor.DisplayName;
            apiItem.AddOperation(OperationType.Post, operation);

            return apiItem;
        }

        public OpenApiResponse CreateResponseBody(MethodDescriptor descriptor)
        {
            var response = new OpenApiResponse
            {
                Description = "Success",
                Content = new Dictionary<string, OpenApiMediaType>()
            };

            var responseApiSchemaItem = new OpenApiSchema();

            var contracData = _resolver.GetDataContractForType(descriptor.OutputType.ClrType);
            if (contracData.DataType == DataType.Boolean || contracData.DataType == DataType.String
                || contracData.DataType == DataType.Integer || contracData.DataType == DataType.Number)
            {
                // Boolean/String/Integer/Number
                responseApiSchemaItem.Type = contracData.DataType.Format();
                responseApiSchemaItem.Format = contracData.DataFormat;
            }
            else if (contracData.DataType == DataType.Array || contracData.DataType == DataType.Dictionary
                || contracData.DataType == DataType.Object)
            {
                responseApiSchemaItem.Type = DataType.Object.Format();

                if (contracData.DataType == DataType.Array)
                {
                    // Array
                    responseApiSchemaItem.Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = contracData.ArrayItemType.Name };
                }
                else if (contracData.DataType == DataType.Dictionary)
                {
                    // Dictionary
                    responseApiSchemaItem.Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = contracData.DictionaryValueType.Name };
                }
                else
                {
                    // Object
                    responseApiSchemaItem.Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = descriptor.OutputType.Name };
                }
            }

            response.Content.Add("applciation/json", new OpenApiMediaType { Schema = responseApiSchemaItem });

            return response;
        }

        private OpenApiRequestBody CreateRequestBody(MethodDescriptor descriptor)
        {
            return new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = descriptor.InputType.Name }
                        }
                    }
                }
            };
        }

        // Todo
        private IDictionary<string, OpenApiSchema> CreateSchemas(IList<MessageDescriptor> descriptors)
        {
            var schemas = new Dictionary<string, OpenApiSchema>();
            //resolver all the proto object in the current descriptor
            var temDic = new Dictionary<string, OpenApiSchema>();

            foreach (var item in descriptors)
            {
                var contract = _resolver.ResolveMessage(item);
                var properties = new Dictionary<string, OpenApiSchema>();
                GetAllOpenApiShemas(contract, properties, temDic);

                var schema = new OpenApiSchema
                {
                    Type = DataType.Object.Format(),
                    Properties = properties,
                    Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = item.ClrType.Name },
                    AdditionalPropertiesAllowed = false
                };

                if (!schemas.ContainsKey(item.ClrType.Name))
                    schemas.Add(item.ClrType.Name, schema);
            }

            UnionDic(schemas, temDic);

            return schemas;
        }

        private void UnionDic(IDictionary<string, OpenApiSchema> first, IDictionary<string, OpenApiSchema> second)
        {
            var firstKeys = first?.Select(s => s.Key) ?? Enumerable.Empty<string>();
            var secondKeys = second?.Select(s => s.Key) ?? Enumerable.Empty<string>();

            var unionResult = firstKeys.Union(secondKeys);

            foreach (var item in unionResult)
            {
                if (!firstKeys.Contains(item))
                {
                    first[item] = second[item];
                }
            }
        }

        private void GetAllOpenApiShemas(DataContract contract, IDictionary<string, OpenApiSchema> properties, IDictionary<string, OpenApiSchema> all)
        {
            if (contract.DataType == DataType.Object)
            {
                if (contract.ObjectProperties != null && contract.ObjectProperties.Any())
                {
                    var temp = contract.ObjectProperties;

                    foreach (var item in temp)
                    {
                        var schema = BuildSchema(item);

                        if (!properties.ContainsKey(item.Name))
                            properties.Add(item.Name, schema);

                        var data = _resolver.GetDataContractFromType(item.MemberType);

                        GetAllOpenApiShemas(data, schema.Properties, all);
                    }
                }
            }
            else if (contract.DataType == DataType.Array)
            {
                var data = _resolver.GetDataContractFromType(contract.ArrayItemType);

                all[contract.ArrayItemType.Name] = new OpenApiSchema();

                if (data.ObjectProperties != null && data.ObjectProperties.Any())
                {
                    var temp = data.ObjectProperties;

                    foreach (var item in temp)
                    {
                        var schema = BuildSchema(item);

                        if (!properties.ContainsKey(item.Name))
                            properties.Add(item.Name, schema);

                        all[contract.ArrayItemType.Name].Properties[item.Name] = schema;

                        var dc = _resolver.GetDataContractFromType(item.MemberType);

                        GetAllOpenApiShemas(dc, schema.Properties, schema.Properties);
                    }
                }
                else if (contract.DataType == DataType.Dictionary)
                {
                    var dicData = _resolver.GetDataContractFromType(contract.DictionaryValueType);

                    all[contract.DictionaryValueType.Name] = new OpenApiSchema();

                    if (data.ObjectProperties != null && data.ObjectProperties.Any())
                    {
                        var temp = data.ObjectProperties;

                        foreach (var item in temp)
                        {
                            var schema = BuildSchema(item);

                            if (!properties.ContainsKey(item.Name))
                                properties.Add(item.Name, schema);

                            all[contract.DictionaryValueType.Name].Properties[item.Name] = schema;

                            var dc = _resolver.GetDataContractFromType(item.MemberType);

                            GetAllOpenApiShemas(dc, schema.Properties, schema.Properties);
                        }
                    }
                }
            }
        }

        private OpenApiSchema CreateArraySchema(DataContract contract)
        {
            var schemaItem = new OpenApiSchema();
            schemaItem.Type = DataType.Array.Format();
            schemaItem.Items = new OpenApiSchema
            {
                Type = contract.ArrayItemType.Name,
                Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = contract.ArrayItemType.Name },
            };

            return schemaItem;
        }

        private OpenApiSchema CreateDictionarySchema(DataContract contract)
        {
            var schemaItem = new OpenApiSchema();
            schemaItem.Type = DataType.Object.Format();
            schemaItem.AdditionalPropertiesAllowed = true;
            schemaItem.AdditionalProperties = new OpenApiSchema { Type = contract.DictionaryValueType.Name };

            return schemaItem;
        }

        private OpenApiSchema BuildSchema(DataProperty property)
        {
            var dataContract = _resolver.GetDataContractFromType(property.MemberType);

            if (dataContract.DataType == DataType.Array)
            {
                return CreateArraySchema(dataContract);
            }
            else if (dataContract.DataType == DataType.Dictionary)
            {
                return CreateDictionarySchema(dataContract);
            }
            else
            {
                var schemaItem = new OpenApiSchema();
                schemaItem.Type = dataContract.DataType.Format();
                return schemaItem;
            }
        }

        public HttpRule CreateHttpRule(MethodDescriptor methodDescriptor)
        {
            var httpRule = methodDescriptor.GetOptions()?.GetExtension(AnnotationsExtensions.Http);

            if (httpRule == null)
            {
                httpRule = new HttpRule();
                httpRule.Post = $"/{methodDescriptor.Service.FullName}/{methodDescriptor.Name}";
                httpRule.Body = "*";
            }

            return httpRule;
        }
    }
}
