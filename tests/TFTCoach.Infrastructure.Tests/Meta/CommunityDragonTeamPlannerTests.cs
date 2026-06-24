using System.Text.Json;
using TFTCoach.Infrastructure.Meta;

namespace TFTCoach.Infrastructure.Tests.Meta;

public sealed class CommunityDragonTeamPlannerTests
{
    private const string SampleJson = """
    {
      "TFTSet17": [
        { "character_id": "TFT17_Aatrox",  "display_name": "Aatrox",         "tier": 1, "team_planner_code": 29 },
        { "character_id": "TFT17_Akali",   "display_name": "Akali",          "tier": 2, "team_planner_code": 13 },
        { "character_id": "TFT17_Nunu",    "display_name": "Nunu & Willump", "tier": 4, "team_planner_code": 57 },
        { "character_id": "TFT17_NoCode",  "display_name": "No Code",        "tier": 3 },
        { "character_id": "TFT17_NoName",  "display_name": "",               "tier": 3, "team_planner_code": 99 }
      ],
      "TFTSet16": [
        { "character_id": "TFT16_Kindred", "display_name": "Kindred", "tier": 5, "team_planner_code": 7 }
      ]
    }
    """;

    private static IReadOnlyDictionary<int, TFTCoach.Domain.Entities.TeamPlannerChampion> BuildSet(string setId)
    {
        using var doc = JsonDocument.Parse(SampleJson);
        return CommunityDragonTeamPlanner.BuildSetMap(doc.RootElement, setId);
    }

    [Fact]
    public void BuildSetMap_ShouldMapCodeToChampion_WhenPresent()
    {
        var map = BuildSet("TFTSet17");

        Assert.Equal("Aatrox", map[29].Name);
        Assert.Equal(1, map[29].Cost);
        Assert.Equal("TFT17_Aatrox", map[29].CharacterId);
        Assert.Equal("Nunu & Willump", map[57].Name);
    }

    [Fact]
    public void BuildSetMap_ShouldSkipChampions_WhenCodeOrNameMissing()
    {
        var map = BuildSet("TFTSet17");

        Assert.DoesNotContain(map.Values, c => c.Name == "No Code");
        Assert.False(map.ContainsKey(99));
    }

    [Fact]
    public void BuildSetMap_ShouldScopeToRequestedSet()
    {
        var set17 = BuildSet("TFTSet17");
        var set16 = BuildSet("TFTSet16");

        Assert.False(set17.ContainsKey(7));
        Assert.Equal("Kindred", set16[7].Name);
    }

    [Fact]
    public void BuildSetMap_ShouldReturnEmpty_WhenSetUnknown()
    {
        var map = BuildSet("TFTSet99");

        Assert.Empty(map);
    }
}
