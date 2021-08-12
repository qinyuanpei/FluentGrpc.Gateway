using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    [Serializable]
    public class GrpcGatewayOptions
    {
        public string BaseUrl { get; set; }

        public List<UpstreamInfo> UpstreamInfos { get; set; } = new List<UpstreamInfo>();

        public IEnumerable<Assembly> GetAssemblies()
        {
            foreach(var upstreamInfo in UpstreamInfos)
            {
                var assemblyFile = $"{upstreamInfo.AssemblyName}.dll";
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyFile);
                yield return Assembly.LoadFrom(filePath);
            }
        }
    }

    [Serializable]
    public class UpstreamInfo
    {
        public string BaseUrl { get; set; }

        public string AssemblyName { get; set; }

        public Assembly GetAssembly()
        {
            var assemblyFile = $"{AssemblyName}.dll";
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyFile);
            return Assembly.LoadFrom(filePath);
        }
    }
}
