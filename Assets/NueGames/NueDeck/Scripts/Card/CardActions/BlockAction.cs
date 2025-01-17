using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class BlockAction : CardActionBase
    {
        public override CardActionType ActionType => CardActionType.Block;
        public override void DoAction(CardActionParameters actionParameters)
        {
            var newTarget = actionParameters.TargetCharacter
                ? actionParameters.TargetCharacter
                : actionParameters.SelfCharacter;
            
            if (!newTarget) return;
            var value = Mathf.RoundToInt(actionParameters.Value); 
            newTarget.characterStats.ApplyStatus(StatusType.Block, value);

            if (FxManager != null)
            {
                FxManager.PlayFx(newTarget.transform, FxType.Block);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,
                    "<Color=#8888ee>" +value +"</color>");
            }
            
        }
    }
}