using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ComunioAdminTools.Core.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum PlayerPosition
{
    [EnumMember(Value = "keeper")]
    Keeper,

    [EnumMember(Value = "defender")]
    Defender,

    [EnumMember(Value = "midfielder")]
    Midfielder,

    [EnumMember(Value = "striker")]
    Striker
}
