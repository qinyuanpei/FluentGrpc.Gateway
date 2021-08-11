using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    public static class GrpcApiDescriptionsProviderExtensions
    {
        public static IEnumerable<ApiDescription> GetApiDescriptions(this IApiDescriptionGroupCollectionProvider apiDescriptionGroupCollectionProvider)
        {
            return apiDescriptionGroupCollectionProvider.ApiDescriptionGroups.Items.SelectMany(x => x.Items);
        }
    }
}
