using NueGames.NueDeck.Scripts.Enums;
using UnityEngine;



namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class CustomEffectAction : CardActionBase
    {
        public override CardActionType ActionType => CardActionType.CustomEffect;
        public override void DoAction(CardActionParameters actionParameters)
        {
            if (!actionParameters.TargetCharacter) return;
            
            var targetCharacter = actionParameters.TargetCharacter;
            var selfCharacter = actionParameters.SelfCharacter;
            
            int value = Mathf.RoundToInt(actionParameters.Value); 
            
            Debug.Log("Custom Effect Action:\n" + actionParameters.StrParam);
            targetCharacter.characterStats.ApplyCustomEffect
                (actionParameters.StrParam, value);

            if (FxManager != null)
            {
                FxManager.PlayFx(actionParameters.TargetCharacter.transform,FxType.Buff);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,
                    actionParameters.StrParam +" " +value);
            }
           
            if (AudioManager != null) 
                AudioManager.PlayOneShot(actionParameters.CardData.AudioType);
        }
    }
}

