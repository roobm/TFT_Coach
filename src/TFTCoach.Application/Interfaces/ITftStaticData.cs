namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Resolves Riot internal API names (e.g. <c>TFT_Item_MadredsBloodrazor</c>,
/// <c>TFT14_Bard</c>, <c>TFT14_FlexTrait</c>) into the human-readable display
/// names players actually see in-game (e.g. <c>Giant Slayer</c>, <c>Bard</c>,
/// <c>Flex</c>). Backed by static game data so meta comps read correctly instead
/// of exposing raw identifiers.
/// </summary>
public interface ITftStaticData
{
    /// <summary>
    /// Loads the static data once (idempotent). Safe to call before resolving;
    /// resolution falls back to a best-effort cleanup if loading failed.
    /// </summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    /// <summary>Display name for an item apiName, or a cleaned fallback.</summary>
    string Item(string apiName);

    /// <summary>Display name for a champion apiName/character_id, or a cleaned fallback.</summary>
    string Champion(string apiName);

    /// <summary>Display name for a trait apiName, or a cleaned fallback.</summary>
    string Trait(string apiName);
}
