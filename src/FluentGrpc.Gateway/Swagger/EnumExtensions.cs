using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FluentGrpc.Gateway.Swagger
{
    public static class EnumExtensions
    {
        public static string Format(this Enum data, FormatType @type = FormatType.ToLower)
        {
            return Get(data, @type).Invoke();

            static Func<string> Get(Enum data, FormatType type)
            {
                var dic = new Dictionary<FormatType, Func<string>>
                {
                    [FormatType.ToLower] = () => data.ToString().ToLower(CultureInfo.InvariantCulture),
                    [FormatType.None] = () => data.ToString().ToString(CultureInfo.InvariantCulture),
                    [FormatType.ToUpper] = () => data.ToString().ToUpper(CultureInfo.InvariantCulture)
                };

                return dic[type];
            }
        }
    }

    public enum FormatType
    {
        ToUpper,
        ToLower,
        None
    }
}
