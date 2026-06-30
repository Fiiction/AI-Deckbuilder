using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class IncreaseStrengthAction : CardActionBase
    {
        public override CardActionType ActionType => CardActionType.IncreaseStrength;
        public override void DoAction(CardActionParameters actionParameters)
        {
            var newTarget = actionParameters.TargetCharacter
                ? actionParameters.TargetCharacter
                : actionParameters.SelfCharacter;

            if (!newTarget) return;

            int value = Mathf.RoundToInt(actionParameters.Value);
            newTarget.characterStats.ApplyStatus(StatusType.Strength, value);

            if (FxManager != null)
            {
                FxManager.PlayFx(newTarget.transform, FxType.Str);
                if (newTarget.TextSpawnRoot)
                {
                    string prefix = value >= 0 ? "+" : "";
                    FxManager.SpawnFloatingText(newTarget.TextSpawnRoot,
                        "<Color=#ffaa55>Strength " + prefix + value + "</color>");
                }
            }
        }
    }
}