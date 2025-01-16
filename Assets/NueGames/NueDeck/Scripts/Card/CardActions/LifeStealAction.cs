using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class LifeStealAction : CardActionBase
    {
        public override CardActionType ActionType => CardActionType.LifeSteal;
        public override void DoAction(CardActionParameters actionParameters)
        {
            if (!actionParameters.TargetCharacter) return;

            int value = Mathf.RoundToInt(actionParameters.Value); 
            actionParameters.TargetCharacter.characterStats.Damage(value);
            actionParameters.SelfCharacter.characterStats.Heal(value);
            
            if (FxManager != null)
            {
                FxManager.PlayFx(actionParameters.TargetCharacter.transform,FxType.Attack);
                FxManager.PlayFx(actionParameters.SelfCharacter.transform,FxType.Heal);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,
                    "<Color=#cc1111><b>Life Steal: " + value +"</b></color>");
            }
           
            if (AudioManager != null) 
                AudioManager.PlayOneShot(actionParameters.CardData.AudioType);
        }
    }
}