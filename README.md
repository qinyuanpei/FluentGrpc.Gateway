# Grpc.Gateway

[中文](https://github.com/qinyuanpei/Grpc.Gateway/blob/master/README_CN.md) | [English](https://github.com/qinyuanpei/Grpc.Gateway/blob/master/README.md)

An extension based on ASP.NET Core endpoint routing that allows you to call gRpc just like a JSON API. And the idea is,

> Generate dynamic routes for each gRPC client through reflection and expression tree, and the `JSON` -> `Protobuf` -> `JSON` transformation is completed by this extension. 

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
For more details, see：[ExampleService](https://github.com/qinyuanpei/Grpc.Gateway/tree/master/src/Example/ExampleService)

* configure your gateway

```csharp
public void ConfigureServices(IServiceCollection services)
{

    // ...
    services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true);
    services.Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
    services.AddGrpcClients(typeof(GreeterService).Assembly, opt =>
    {
        opt.Address = new Uri("https://localhost:8001");
    });
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // ...
    app.UseGrpcGateway
    (
        typeof(GreeterService).Assembly
    );
}
```

For more details, see：[ExampleGateway](https://github.com/qinyuanpei/Grpc.Gateway/tree/master/src/Example/ExampleGateway)

* Consume your service

For the `SayHelloAsync()` method of the gRPC Client `Greeter.GreeterClient`, the default route generated is: `/Greeter/SayHello`, which removes the `Client` and `Async` parts.  

At this point, we just need to use Postman or crul to consume the interface. Enjoy :)  




