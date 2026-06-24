namespace TFTCoach.Domain.Entities;

/// <summary>
/// Category of a live coaching suggestion. Maps one-to-one to the tag that
/// every suggestion must lead with: <c>[ECONOMY] [BOARD] [ITEMS] [COMP] [SCOUT]</c>.
/// </summary>
public enum SuggestionCategory
{
    Economy,
    Board,
    Items,
    Comp,
    Scout,
}
