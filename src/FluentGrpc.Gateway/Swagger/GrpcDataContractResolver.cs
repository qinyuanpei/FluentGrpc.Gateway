using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Swashbuckle.AspNetCore.SwaggerGen;
using Type = System.Type;

namespace FluentGrpc.Gateway.Swagger
{
    public sealed class GrpcDataContractResolver : ISerializerDataContractResolver
    {
        private readonly Dictionary<Type, EnumDescriptor> _enumTypeMapping;
        private readonly Dictionary<Type, MessageDescriptor> _messageTypeMapping;
        private readonly ISerializerDataContractResolver _innerContractResolver;

        private static readonly HashSet<string> WellKnownTypeNames = new HashSet<string>
        {
            "google/protobuf/any.proto",
            "google/protobuf/api.proto",
            "google/protobuf/duration.proto",
            "google/protobuf/empty.proto",
            "google/protobuf/wrappers.proto",
            "google/protobuf/timestamp.proto",
            "google/protobuf/field_mask.proto",
            "google/protobuf/source_context.proto",
            "google/protobuf/struct.proto",
            "google/protobuf/type.proto",
        };

        public GrpcDataContractResolver(ISerializerDataContractResolver innerContractResolver)
        {
            _messageTypeMapping = new Dictionary<Type, MessageDescriptor>();
            _enumTypeMapping = new Dictionary<Type, EnumDescriptor>();
            _innerContractResolver = innerContractResolver;
        }

        public DataContract GetDataContractFromType(Type type)
        {
            if (type.IsEnum)
            {
                if (_enumTypeMapping.TryGetValue(type, out var enumDescriptor))
                {
                    var values = enumDescriptor.Values.Select(v => v.Name).ToList();
                    return DataContract.ForPrimitive(type, DataType.Integer, dataFormat: null, enumValues: values);
                }
            }

            if (typeof(IMessage).IsAssignableFrom(type))
            {
                var property = type.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
                var messageDescriptor = property?.GetValue(null) as MessageDescriptor;

                if (messageDescriptor == null)
                {
                    throw new InvalidOperationException($"Couldn't resolve message descriptor for {type}.");
                }

                return ConvertMessage(messageDescriptor);
            }

            return _innerContractResolver.GetDataContractForType(type);
        }

        public DataContract GetDataContractForType(Type type)
        {
            if (!_messageTypeMapping.TryGetValue(type, out var messageDescriptor))
            {
                if (typeof(IMessage).IsAssignableFrom(type))
                {
                    var property = type.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
                    messageDescriptor = property?.GetValue(null) as MessageDescriptor;

                    if (messageDescriptor == null)
                    {
                        throw new InvalidOperationException($"Couldn't resolve message descriptor for {type}.");
                    }

                    _messageTypeMapping[type] = messageDescriptor;
                }
            }

            if (messageDescriptor != null)
            {
                return ConvertMessage(messageDescriptor);
            }

            if (type.IsEnum)
            {
                if (_enumTypeMapping.TryGetValue(type, out var enumDescriptor))
                {
                    var values = enumDescriptor.Values.Select(v => v.Name).ToList();
                    return DataContract.ForPrimitive(type, DataType.String, dataFormat: null, enumValues: values);
                }
            }

            return _innerContractResolver.GetDataContractForType(type);
        }

        public Type ResolveFieldType(FieldDescriptor field)
        {
            switch (field.FieldType)
            {
                case FieldType.Double:
                    return typeof(double);
                case FieldType.Float:
                    return typeof(float);
                case FieldType.Int64:
                    return typeof(long);
                case FieldType.UInt64:
                    return typeof(ulong);
                case FieldType.Int32:
                    return typeof(int);
                case FieldType.Fixed64:
                    return typeof(long);
                case FieldType.Fixed32:
                    return typeof(int);
                case FieldType.Bool:
                    return typeof(bool);
                case FieldType.String:
                    return typeof(string);
                case FieldType.Bytes:
                    return typeof(string);
                case FieldType.UInt32:
                    return typeof(uint);
                case FieldType.SFixed32:
                    return typeof(int);
                case FieldType.SFixed64:
                    return typeof(long);
                case FieldType.SInt32:
                    return typeof(int);
                case FieldType.SInt64:
                    return typeof(long);
                case FieldType.Enum:
                    return field.EnumType.ClrType;
                case FieldType.Message:
                    return field.MessageType.ClrType;
                default:
                    throw new InvalidOperationException("Unexpected field type: " + field.FieldType);
            }
        }

        public DataContract ConvertMessage(MessageDescriptor messageDescriptor)
        {
            if (IsWellKnownType(messageDescriptor))
            {
                if (IsWrapperType(messageDescriptor))
                {
                    var field = messageDescriptor.Fields[Int32Value.ValueFieldNumber];

                    var tp = ResolveFieldType(field);
                  
                    var anyProperties = new List<DataProperty>
                    {
                        new DataProperty("value", tp, isRequired: true)
                    };

                    return DataContract.ForObject(tp, anyProperties, extensionDataType: typeof(Value));
                }

                if (messageDescriptor.FullName == Timestamp.Descriptor.FullName ||
                    messageDescriptor.FullName == Duration.Descriptor.FullName ||
                    messageDescriptor.FullName == FieldMask.Descriptor.FullName)
                {
                    return DataContract.ForPrimitive(messageDescriptor.ClrType, DataType.String, dataFormat: null);
                }
                if (messageDescriptor.FullName == Struct.Descriptor.FullName)
                {
                    return DataContract.ForObject(messageDescriptor.ClrType, Array.Empty<DataProperty>(), extensionDataType: typeof(Value));
                }
                if (messageDescriptor.FullName == ListValue.Descriptor.FullName)
                {
                    return DataContract.ForArray(messageDescriptor.ClrType, typeof(Value));
                }
                if (messageDescriptor.FullName == Value.Descriptor.FullName)
                {
                    return DataContract.ForPrimitive(messageDescriptor.ClrType, DataType.Unknown, dataFormat: null);
                }
                if (messageDescriptor.FullName == Any.Descriptor.FullName)
                {
                    var anyProperties = new List<DataProperty>
                    {
                        new DataProperty("@type", typeof(string), isRequired: true)
                    };
                    return DataContract.ForObject(messageDescriptor.ClrType, anyProperties, extensionDataType: typeof(Value));
                }
            }

            var properties = new List<DataProperty>();

            foreach (var field in messageDescriptor.Fields.InFieldNumberOrder())
            {
                // Enum type will later be used to call this contract resolver.
                // Register the enum type so we know to resolve its names from the descriptor.
                if (field.FieldType == FieldType.Enum)
                {
                    _enumTypeMapping.TryAdd(field.EnumType.ClrType, field.EnumType);
                }

                Type fieldType;
                if (field.IsMap)
                {
                    var mapFields = field.MessageType.Fields.InFieldNumberOrder();
                    var valueType = ResolveFieldType(mapFields[1]);
                    fieldType = typeof(IDictionary<,>).MakeGenericType(typeof(string), valueType);
                }
                else if (field.IsRepeated)
                {
                    fieldType = typeof(IList<>).MakeGenericType(ResolveFieldType(field));
                }
                else
                {
                    fieldType = ResolveFieldType(field);
                }

                properties.Add(new DataProperty(field.JsonName, fieldType));
            }

            var schema = DataContract.ForObject(messageDescriptor.ClrType, properties: properties);

            return schema;
        }

        internal bool IsWellKnownType(MessageDescriptor messageDescriptor) => messageDescriptor.File.Package == "google.protobuf" &&
            WellKnownTypeNames.Contains(messageDescriptor.File.Name);

        internal bool IsWrapperType(MessageDescriptor messageDescriptor) => messageDescriptor.File.Package == "google.protobuf" &&
            messageDescriptor.File.Name == "google/protobuf/wrappers.proto";
    }
}
