using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ComunioAdminTools.Core.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum PlayerStatus
{
    [EnumMember(Value = "ACTIVE")]
    Active,

    [EnumMember(Value = "AWAY")]
    Away,

    [EnumMember(Value = "DECEASED")]
    Deceased,

    [EnumMember(Value = "GAME_BREAK")]
    GameBreak,

    [EnumMember(Value = "INJURED")]
    Injured,

    [EnumMember(Value = "MISCELLANEOUS")]
    Miscellaneous,

    [EnumMember(Value = "RED_BANNED")]
    RedBanned,

    [EnumMember(Value = "REHABILITATION")]
    Rehabilitation,

    [EnumMember(Value = "RETIRED")]
    Retired,

    [EnumMember(Value = "SUSPENDED")]
    Suspended,

    [EnumMember(Value = "WEAKENED")]
    Weakened,

    [EnumMember(Value = "YELLOW_BANNED")]
    YellowBanned,

    [EnumMember(Value = "YELLOW_RED_BANNED")]
    YellowRedBanned
}
