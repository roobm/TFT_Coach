using TFTCoach.Infrastructure.RiotApi;

namespace TFTCoach.Infrastructure.Tests.RiotApi;

public class PatchNoteChunkerTests
{
    [Fact]
    public void Champion_ShouldIncludeCostTraitsAndAbility_WhenAllProvided()
    {
        var text = PatchNoteChunker.Champion(
            name: "Ahri",
            cost: 4,
            traits: ["Sorcerer", "Spirit"],
            abilityName: "Spirit Bomb",
            abilityDescription: "Deals magic damage\nto enemies.");

        Assert.Contains("Champion: Ahri.", text);
        Assert.Contains("Cost: 4.", text);
        Assert.Contains("Traits: Sorcerer, Spirit.", text);
        Assert.Contains("Ability: Spirit Bomb.", text);
        Assert.Contains("Deals magic damage to enemies.", text);
        Assert.DoesNotContain("\n", text);
    }

    [Fact]
    public void Trait_ShouldCollapseWhitespaceAndMarkup_WhenDescriptionHasTags()
    {
        var text = PatchNoteChunker.Trait("Sorcerer", "Gain   AP.<br>Scales with tier.");

        Assert.Equal("Trait: Sorcerer. Gain AP. Scales with tier.", text);
    }

    [Fact]
    public void Item_ShouldReturnNameOnly_WhenDescriptionMissing()
    {
        var text = PatchNoteChunker.Item("Deathblade", null);

        Assert.Equal("Item: Deathblade.", text);
    }
}
