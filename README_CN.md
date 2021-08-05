# Grpc.Gateway

[中文](https://github.com/qinyuanpei/Grpc.Gateway/blob/master/README_CN.md) | [English](https://github.com/qinyuanpei/Grpc.Gateway/blob/master/README.md)

一个基于 `ASP.NET Core` 终结点路由打造的扩展，可以让你像调用一个 `JSON API` 一样调用 `gRpc`。其原理是，

> 通过反射和表达式树为每一个 `gRPC` 客户端生成动态路由，并由该扩展完成 `JSON` -> `Protobuf` -> `JSON` 的转换。

# 如何使用

* 编写服务

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

请确保该项目可以同时生成 `gRPC` 客户端和服务端的代码，因为网关中需要用到 `gRPC` 客户端。

```xml
<ItemGroup>
    <Protobuf Include="Protos\greet.proto" GrpcServices="Both" />
</ItemGroup>
```
更多细节，请参考：[ExampleService](https://github.com/qinyuanpei/Grpc.Gateway/tree/master/src/Example/ExampleService)

* 配置网关

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

更多细节，请参考：[ExampleGateway](https://github.com/qinyuanpei/Grpc.Gateway/tree/master/src/Example/ExampleGateway)

* 调用接口

对于 `gRPC` 客户端 `Greeter.GreeterClient` 的 `SayHelloAsync()` 方法，其生成的默认路由为：`/Greeter/SayHello`，即移除`Client` 和 `Async` 部分。

此时，我们只需要使用 `Postman` 或者 `crul` 以 `POST` 方式调用接口即可，Enjoy :)

![像调用一个 JSON API 一样调用 gRpc](https://raw.fastgit.org/qinyuanpei/Grpc.Gateway/master/src/Example/Screenshots/Apifox.png)

