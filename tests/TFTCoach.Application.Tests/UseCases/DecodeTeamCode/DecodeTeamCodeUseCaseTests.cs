using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Common;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.DecodeTeamCode;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Tests.UseCases.DecodeTeamCode;

public class DecodeTeamCodeUseCaseTests
{
    // The official team_planner_code → champion mapping for the codes used below.
    private static readonly Dictionary<int, TeamPlannerChampion> Set17 = new()
    {
        [29] = Champ("TFT17_Aatrox", "Aatrox", 1),
        [13] = Champ("TFT17_Akali", "Akali", 2),
        [33] = Champ("TFT17_Rhaast", "Rhaast", 3),
        [57] = Champ("TFT17_Nunu", "Nunu & Willump", 4),
        [59] = Champ("TFT17_Shen", "Shen", 5),
        [56] = Champ("TFT17_Blitzcrank", "Blitzcrank", 5),
        [19] = Champ("TFT17_Fiora", "Fiora", 5),
        [80] = Champ("TFT17_Graves", "Graves", 5),
        [58] = Champ("TFT17_Vex", "Vex", 5),
    };

    [Fact]
    public async Task ExecuteAsync_ShouldDecodeChampionsInOrder_WhenCodeIsValid()
    {
        // Arrange
        var useCase = NewUseCase(Set17);

        // Act
        var result = await useCase.ExecuteAsync(
            new DecodeTeamCodeRequest { Code = "0201d00d02103903b03801305003a000TFTSet17" });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal("TFTSet17", response.Set);
        Assert.Equal(
            new[] { "Aatrox", "Akali", "Rhaast", "Nunu & Willump", "Shen", "Blitzcrank", "Fiora", "Graves", "Vex" },
            response.Units.Select(u => u.Name).ToArray());
        Assert.Equal(1, response.Units[0].Cost);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreEmptySlots_WhenCodeHasZeroChunks()
    {
        // Arrange — only the first two champions, the rest are empty (000) slots.
        var useCase = NewUseCase(Set17);

        // Act
        var result = await useCase.ExecuteAsync(
            new DecodeTeamCodeRequest { Code = "0201d00d000000TFTSet17" });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal(new[] { "Aatrox", "Akali" }, response.Units.Select(u => u.Name).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipUnknownCodes_WhenChampionMissing()
    {
        // Arrange — code 0xfff (4095) is not in the map.
        var useCase = NewUseCase(Set17);

        // Act
        var result = await useCase.ExecuteAsync(
            new DecodeTeamCodeRequest { Code = "0201dfffTFTSet17" });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal(new[] { "Aatrox" }, response.Units.Select(u => u.Name).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenCodeHasNoSetSuffix()
    {
        // Arrange
        var useCase = NewUseCase(Set17);

        // Act
        var result = await useCase.ExecuteAsync(new DecodeTeamCodeRequest { Code = "0201d00d" });

        // Assert
        Assert.False(result.Success);
        Assert.Equal("validation.invalid_team_code", result.Error?.Code);
    }

    private static DecodeTeamCodeUseCase NewUseCase(IReadOnlyDictionary<int, TeamPlannerChampion> map) =>
        new(new FakeTeamPlanner(map), NullLogger<DecodeTeamCodeUseCase>.Instance);

    private static T AssertSuccess<T>(RequestResult<T> result)
    {
        Assert.True(result.Success);
        Assert.Null(result.Error);
        return Assert.IsType<T>(result.Data);
    }

    private static TeamPlannerChampion Champ(string id, string name, int cost) =>
        new() { CharacterId = id, Name = name, Cost = cost };

    private sealed class FakeTeamPlanner(IReadOnlyDictionary<int, TeamPlannerChampion> map) : ITftTeamPlanner
    {
        public Task<IReadOnlyDictionary<int, TeamPlannerChampion>> GetChampionsAsync(
            string setId, CancellationToken cancellationToken = default) => Task.FromResult(map);
    }
}
