using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Services;

namespace TarteelClone.Api;

public sealed record ApiStatus(bool IsReady, string Version, string Message);

public interface ILocalApi
{
    ApiStatus GetStatus();
    MadrasaEvaluation EvaluateRecitation(MadrasaEvaluationInput input);
    IReadOnlyList<MutashabihaChallenge> GetMutashabihatChallenges();
    MutashabihaChallenge GetRandomExamChallenge();
}
