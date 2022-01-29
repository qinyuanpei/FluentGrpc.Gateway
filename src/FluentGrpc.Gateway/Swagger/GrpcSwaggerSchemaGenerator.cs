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

        public IDictionary<string, OpenApiSchema> GenerateSchemas(IEnumerable<ApiDescription> apiDescriptions)
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
            operation.Tags = new List<OpenApiTag> {
                new OpenApiTag() {
                    Name = methodDescriptor.Service.FullName,
                    Description = ""
                }
            };
            operation.Responses.Add("200", CreateResponseBody(methodDescriptor));
            operation.RequestBody = CreateRequestBody(methodDescriptor);
            operation.Description = apiDescription.ActionDescriptor.DisplayName;
            apiItem.AddOperation(OperationType.Post, operation);

            return apiItem;
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

        public OpenApiResponse CreateResponseBody(MethodDescriptor descriptor)
        {
            var response = new OpenApiResponse
            {
                Description = "Success",
                Content = new Dictionary<string, OpenApiMediaType>()
            };

            var responseApiSchemaItem = new OpenApiSchema();
            DataContract contracData = null;
            if (descriptor.OutputType.ClrType == null)
            {
                contracData = _resolver.ResolveMessage(descriptor.OutputType);
            }
            else
            {
                contracData = _resolver.GetDataContractForType(descriptor.OutputType.ClrType);
            }
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

        private IDictionary<string, OpenApiSchema> CreateSchemas(IList<MessageDescriptor> descriptors)
        {
            var currentSchemas = new Dictionary<string, OpenApiSchema>();
            var referedSchemas = new Dictionary<string, OpenApiSchema>();

            foreach (var item in descriptors)
            {
                var contract = _resolver.ResolveMessage(item);
                var properties = new Dictionary<string, OpenApiSchema>();
                ResolveOpenApiSchemas(contract, properties, referedSchemas);

                var schema = new OpenApiSchema
                {
                    Type = DataType.Object.Format(),
                    Properties = properties,
                    Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = item.ClrType == null?item.Name:item.ClrType.Name },
                    AdditionalPropertiesAllowed = false
                };

                if (item.ClrType != null && !currentSchemas.ContainsKey(item.ClrType.Name))
                    currentSchemas.Add(item.ClrType.Name, schema);
                else if (!currentSchemas.ContainsKey(item.Name))
                {
                    currentSchemas.Add(item.Name, schema);
                }
            }

            return UnionSchemas(currentSchemas,referedSchemas);
        }

        private IDictionary<string, OpenApiSchema> UnionSchemas(IDictionary<string, OpenApiSchema> left, IDictionary<string, OpenApiSchema> right)
        {
            var leftKeys = left?.Select(s => s.Key) ?? Enumerable.Empty<string>();
            var rightKeys = right?.Select(s => s.Key) ?? Enumerable.Empty<string>();

            var unionKeys = leftKeys.Union(rightKeys);

            foreach (var key in unionKeys)
            {
                if (!leftKeys.Contains(key))
                    left[key] = right[key];
            }

            return left;
        }

        private void ResolveOpenApiSchemas(DataContract contract, IDictionary<string, OpenApiSchema> propertiesSchema, IDictionary<string, OpenApiSchema> referedSchemas)
        {
            if (contract.DataType == DataType.Object)
            {
                if (contract.ObjectProperties != null && contract.ObjectProperties.Any())
                {
                    foreach (var dataProperty in contract.ObjectProperties)
                    {
                        var propertySchema = CreateSchemaByDataProperty(dataProperty);

                        // Property
                        if (!propertiesSchema.ContainsKey(dataProperty.Name))
                            propertiesSchema.Add(dataProperty.Name, propertySchema);

                        // Property Schema
                        var memberContract = _resolver.GetDataContractFromType(dataProperty.MemberType);
                        ResolveOpenApiSchemas(memberContract, propertySchema.Properties, referedSchemas);
                    }
                }
            }
            else if (contract.DataType == DataType.Array)
            {
                var arrayContract = _resolver.GetDataContractFromType(contract.ArrayItemType);
                referedSchemas[contract.ArrayItemType.Name] = new OpenApiSchema();

                if (arrayContract.ObjectProperties != null && arrayContract.ObjectProperties.Any())
                {
                    foreach (var dataProperty in arrayContract.ObjectProperties)
                    {
                        var propertySchema = CreateSchemaByDataProperty(dataProperty);

                        // Property
                        if (!propertiesSchema.ContainsKey(dataProperty.Name))
                            propertiesSchema.Add(dataProperty.Name, propertySchema);

                        // Refered Scheam
                        referedSchemas[contract.ArrayItemType.Name].Properties[dataProperty.Name] = propertySchema;

                        // Property Schema
                        var memeberContract = _resolver.GetDataContractFromType(dataProperty.MemberType);
                        ResolveOpenApiSchemas(memeberContract, propertySchema.Properties, propertiesSchema);
                    }
                }
            }
            else if (contract.DataType == DataType.Dictionary)
            {
                var dictContract = _resolver.GetDataContractFromType(contract.DictionaryValueType);
                referedSchemas[contract.DictionaryValueType.Name] = new OpenApiSchema();

                if (dictContract.ObjectProperties != null && dictContract.ObjectProperties.Any())
                {
                    foreach (var dataProperty in dictContract.ObjectProperties)
                    {
                        var propertySchema = CreateSchemaByDataProperty(dataProperty);

                        // Property
                        if (!propertiesSchema.ContainsKey(dataProperty.Name))
                            propertiesSchema.Add(dataProperty.Name, propertySchema);

                        // Refered Scheam
                        referedSchemas[contract.DictionaryValueType.Name].Properties[dataProperty.Name] = propertySchema;

                        // Property Schema
                        var memeberContract = _resolver.GetDataContractFromType(dataProperty.MemberType);
                        ResolveOpenApiSchemas(memeberContract, propertySchema.Properties, propertySchema.Properties);
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

        private OpenApiSchema CreateSchemaByDataProperty(DataProperty property)
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
