# FluentGrpc.Gateway

![GitHub](https://img.shields.io/github/license/qinyuanpei/FluentGrpc.Gateway) ![GitHub Workflow Status](https://img.shields.io/github/workflow/status/qinyuanpei/FluentGrpc.Gateway/Release) ![Nuget](https://img.shields.io/nuget/v/FluentGrpc.Gateway)

![FluentGrpc.Gateway](https://raw.fastgit.org/qinyuanpei/FluentGrpc.Gateway/master/example/Screenshots/FluentGrpc.Gateway.png)

[中文](https://github.com/qinyuanpei/FluentGrpc.Gateway/blob/master/README_CN.md) | [English](https://github.com/qinyuanpei/FluentGrpc.Gateway/blob/master/README.md)

一个基于 `ASP.NET Core` 终结点路由打造的 `gRPC` 扩展，可以让你像调用一个 `JSON API` 一样调用 `gRPC`。

> 通过反射和表达式树，为每一个 `gRPC` 客户端动态生成 [终结点路由](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-5.0) ，并由该扩展完成从 `JSON` 到 `Protobuf` 再到 `JSON` 的转换。

与此同时，实现了 从 `Protobuf` 到 `Swagger`，即 [OpenAPI](https://swagger.io/specification/) 规范的转换。

# 安装方法

```
dotnet add package FluentGrpc.Gateway
```

# 主要特性

* [x] 服务代理： 像调用一个 `JSON API` 一样调用 `gRPC`，类似于 [Envoy](https://www.envoyproxy.io/)  的 [gRPC-JSON Transcoder](https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_filters/grpc_json_transcoder_filter)。
* [x] 接口文档：通过 Swagger 查阅和调试 `gRPC` 接口。

# 基本使用

## 通过 Protobuf 定义 gRPC 服务

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
更多细节，请参考：[GreetGrpc](https://github.com/qinyuanpei/FluentGrpc.Gateway/tree/master/example/GreetGrpc)

## 配置 gRPC 网关

目前支持下面两种模式：

### 聚合模式

在入口文件 `Startup.cs` 中添加如下配置：

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

在配置文件 `appsettings.json` 中添加如下配置：

```json
"GrpcGateway": {
    "BaseUrl": "https://lcoalhost:5001",
    "UrlPrefix": "api",
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

### Sidecar 模式

在入口文件 `Startup.cs` 中添加如下配置：

```csharp
public void ConfigureServices(IServiceCollection services)
{

    // ...
    services.AddGrpc();
    services.AddGrpcGateway(Configuration);
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // ...
    app.UseGrpcGateway();
}
```


更多细节，请参考：[ExampleGateway](https://github.com/qinyuanpei/FluentGrpc.Gateway/tree/master/example/ExampleGateway)。

## 像 JSON API 一样消费 gRPC 服务

如果希望浏览基于 Swaagger 的 API 文档，可以在浏览器中输入下列地址：`https://localhost:5001//swagger/index.html`。

对于 `gRPC` 客户端 `Greeter.GreeterClient` 的 `SayHelloAsync()` 方法，其生成的默认路由为：`/greet.Greeter/SayHello`。

此时，我们只需要使用 `Postman` 或者 `crul` 以 `POST` 方式调用接口即可，Enjoy :)

![像调用一个 JSON API 一样调用 gRpc](https://raw.fastgit.org/qinyuanpei/FluentGrpc.Gateway/master/example/Screenshots/Swagger.png)

