using System.Text.Json;
using TFTCoach.Infrastructure.Meta;

namespace TFTCoach.Infrastructure.Tests.Meta;

public sealed class CommunityDragonStaticDataTests
{
    private const string SampleJson = """
    {
      "items": [
        { "apiName": "TFT_Item_MadredsBloodrazor", "name": "Giant Slayer" },
        { "apiName": "TFT_Item_JeweledGauntlet", "name": "Jeweled Gauntlet" },
        { "apiName": "TFT_Item_EmptyName", "name": "" }
      ],
      "setData": [
        {
          "number": 14,
          "champions": [ { "apiName": "TFT14_Bard", "name": "Bard" } ],
          "traits": [ { "apiName": "TFT14_FlexTrait", "name": "Flex" } ]
        },
        {
          "number": 17,
          "champions": [ { "apiName": "TFT17_Jhin", "name": "Jhin" } ],
          "traits": [ { "apiName": "TFT17_Sniper", "name": "Sniper" } ]
        }
      ]
    }
    """;

    private static (
        IReadOnlyDictionary<string, string> Items,
        IReadOnlyDictionary<string, string> Champions,
        IReadOnlyDictionary<string, string> Traits) BuildMaps()
    {
        using var doc = JsonDocument.Parse(SampleJson);
        return CommunityDragonStaticData.BuildMaps(doc.RootElement);
    }

    [Fact]
    public void BuildMaps_ShouldMapItemApiNameToDisplayName_WhenPresent()
    {
        var maps = BuildMaps();

        Assert.Equal("Giant Slayer", maps.Items["TFT_Item_MadredsBloodrazor"]);
        Assert.Equal("Jeweled Gauntlet", maps.Items["TFT_Item_JeweledGauntlet"]);
    }

    [Fact]
    public void BuildMaps_ShouldSkipEntries_WhenNameIsEmpty()
    {
        var maps = BuildMaps();

        Assert.False(maps.Items.ContainsKey("TFT_Item_EmptyName"));
    }

    [Fact]
    public void BuildMaps_ShouldMapChampionsAndTraitsAcrossAllSets()
    {
        var maps = BuildMaps();

        Assert.Equal("Bard", maps.Champions["TFT14_Bard"]);
        Assert.Equal("Jhin", maps.Champions["TFT17_Jhin"]);
        Assert.Equal("Flex", maps.Traits["TFT14_FlexTrait"]);
        Assert.Equal("Sniper", maps.Traits["TFT17_Sniper"]);
    }

    [Fact]
    public void BuildMaps_ShouldKeyCaseInsensitively()
    {
        var maps = BuildMaps();

        Assert.Equal("Giant Slayer", maps.Items["tft_item_madredsbloodrazor"]);
    }

    [Theory]
    [InlineData("TFT_Item_GiantSlayer", "Giant Slayer")]
    [InlineData("TFT14_Bard", "Bard")]
    [InlineData("TFT9_Item_PsyOpsSympatheticImplant", "Item Psy Ops Sympathetic Implant")]
    public void Clean_ShouldStripPrefixesAndSplitCamelCase_WhenIdUnknown(string raw, string expected)
    {
        Assert.Equal(expected, CommunityDragonStaticData.Clean(raw));
    }
}
