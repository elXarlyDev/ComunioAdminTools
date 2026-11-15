using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ComunioAdminTools.Core.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum LineupFormation
{
    [EnumMember(Value = "442")]
    FourFourTwo = 442,

    [EnumMember(Value = "343")]
    ThreeFourThree = 343,

    [EnumMember(Value = "352")]
    ThreeFiveTwo = 352,

    [EnumMember(Value = "433")]
    FourThreeThree = 433,

    [EnumMember(Value = "451")]
    FourFiveOne = 451
}
