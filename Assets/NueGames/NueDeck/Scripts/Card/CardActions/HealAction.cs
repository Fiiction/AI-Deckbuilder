using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class HealAction: CardActionBase
    {
        public override CardActionType ActionType => CardActionType.Heal;

        public override void DoAction(CardActionParameters actionParameters)
        {
            int value = Mathf.RoundToInt(actionParameters.Value); 
            var newTarget = actionParameters.TargetCharacter
                ? actionParameters.TargetCharacter
                : actionParameters.SelfCharacter;

            if (!newTarget) return;
            
            newTarget.characterStats.Heal(value);

            if (FxManager != null)
            {
                FxManager.PlayFx(newTarget.transform, FxType.Heal);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,
                    "<Color=#77f288>Heal: " + value +"</color>");
            }
            
            if (AudioManager != null) 
                AudioManager.PlayOneShot(actionParameters.CardData.AudioType);
        }
    }
}