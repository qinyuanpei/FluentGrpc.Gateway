using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    public static class EnumFormatterExtensions
    {
        private static Dictionary<FormatType, Func<Enum, string>> _enumFormatterMapping
            = new Dictionary<FormatType, Func<Enum, string>>()
            {
                [FormatType.None] = data => data.ToString().ToString(CultureInfo.InvariantCulture),
                [FormatType.ToLower] = data => data.ToString().ToLower(CultureInfo.InvariantCulture),
                [FormatType.ToUpper] = data => data.ToString().ToUpper(CultureInfo.InvariantCulture)
            };
        public static string Format(this Enum data, FormatType @type = FormatType.ToLower) => _enumFormatterMapping[type].Invoke(data);
    }

    public enum FormatType
    {
        ToUpper,
        ToLower,
        None
    }
}
