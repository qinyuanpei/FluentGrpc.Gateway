# FluentGrpc.Gateway

![GitHub](https://img.shields.io/github/license/qinyuanpei/FluentGrpc.Gateway) ![GitHub Workflow Status](https://img.shields.io/github/workflow/status/qinyuanpei/FluentGrpc.Gateway/Release) ![Nuget](https://img.shields.io/nuget/v/FluentGrpc.Gateway)

[中文](https://github.com/qinyuanpei/FluentGrpc.Gateway/blob/master/README_CN.md) | [English](https://github.com/qinyuanpei/FluentGrpc.Gateway/blob/master/README.md)

An extension based on `ASP.NET Core` endpoint routing that allows you to call `gRPC` just like a `JSON API`. And the idea is,

> Generate dynamic routes for each gRPC client through reflection and expression tree, and the `JSON` -> `Protobuf` -> `JSON` transformation is completed by this extension. 

At the same time, a conversion from Protobuf to Swagger, the [OpenAPI](https://swagger.io/specification/) specification, is currently implemented to facilitate access to the parameters and return values of each gRPC service.  

# Main features

* [x] Gateway for gRPC： Call `gRPC` like a `JSON API`,  Similar to [gRPC-JSON-Transcoder](https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_filters/grpc_json_transcoder_filter) of [Envoy](https://www.envoyproxy.io/)
* [x] Swagger for gRPC：review and debug the `gRPC` interface with Swagger

# How to use it

* Writre your service

```
syntax = "proto3";

option csharp_namespace = "ExampleService";

package greet;

// The greeting service definition.
service Greeter {
  // Sends a greeting
  rpc SayHello (HelloRequest) returns (HelloReply);
}

// The request message containing the user's name.
message HelloRequest {
  string name = 1;
}

// The response message containing the greetings.
message HelloReply {
  string message = 1;
}
```
Make sure that the project can generate code for both the gRPC client and the server, because the gRPC client will be used in the gateway.  

```xml
<ItemGroup>
    <Protobuf Include="Protos\greet.proto" GrpcServices="Both" />
</ItemGroup>
```
For more details, see：[GreetGrpc](https://github.com/qinyuanpei/FluentGrpc.Gateway/tree/master/example/GreetGrpc)

* configure your gateway

Install `FluentGrpc.Gateway` via NuGet 

```csharp
public void ConfigureServices(IServiceCollection services)
{

    // ...
    services.AddGrpcGateway(Configuration);
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // ...
    app.UseGrpcGateway();
}
```
Add the following configuration to the configuration file `appsettings.json`：

```json
"GrpcGateway": {
    "BaseUrl": "https://lcoalhost:5001",
    "UpstreamInfos": [
      {
        "BaseUrl": "https://localhost:8001",
        "AssemblyName": "CalculateGrpc"
      },
      {
        "BaseUrl": "https://localhost:7001",
        "AssemblyName": "GreetGrpc"
      }
    ]
  }
```

For more details, see：[ExampleGateway](https://github.com/qinyuanpei/FluentGrpc.Gateway/tree/master/example/ExampleGateway)

* Consume your service

For the `SayHelloAsync()` method of the gRPC Client `Greeter.GreeterClient`, the default route generated is: `/greet.Greeter/SayHello`.  

At this point, we just need to use Postman or crul to consume the interface. Enjoy :)  

![Call gRpc just like a JSON API](https://raw.fastgit.org/qinyuanpei/FluentGrpc.Gateway/tree/master/example/Screenshots/Swagger.png)




