using Google.Protobuf;
using Google.Protobuf.Reflection;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluentGrpc.Gateway.ApiDescriptors
{
    public class BuffedFileProto
    {
        public string Name => Proto.Name;
        public ByteString Buffer { get; set; }
        public FileDescriptorProto Proto { get; set; }
    }
}
