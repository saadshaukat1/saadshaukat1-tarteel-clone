using TarteelClone.Api;
using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Services;

namespace TarteelMobile.Services;

/// <summary>
/// Offline local API implementing Madrasa pedagogical evaluations and Mutashabihat challenges.
/// </summary>
public sealed class LocalApiService : ILocalApi
{
    public const string Version = "2.0.0-MadrasaTeacher";

    public ApiStatus GetStatus()
        => new(true, Version, "Al-Ustadh Madrasa AI Teacher Engine Ready");

    public MadrasaEvaluation EvaluateRecitation(MadrasaEvaluationInput input)
    {
        return MadrasaPedagogyEngine.EvaluateRecitation(input);
    }

    public IReadOnlyList<MutashabihaChallenge> GetMutashabihatChallenges()
    {
        return MutashabihatDatabase.GetAllChallenges();
    }

    public MutashabihaChallenge GetRandomExamChallenge()
    {
        return MutashabihatDatabase.GetRandomChallenge();
    }
}
