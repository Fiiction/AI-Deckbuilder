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
            
            var value = actionParameters.Value + selfCharacter.characterStats.StatusDict[StatusType.Strength].StatusValue; 
            
            Debug.Log("Custom Effect Action:\n" + actionParameters.StrParam);
            targetCharacter.characterStats.ApplyCustomEffect
                (actionParameters.StrParam, Mathf.RoundToInt(actionParameters.Value));

            if (FxManager != null)
            {
                FxManager.PlayFx(actionParameters.TargetCharacter.transform,FxType.Attack);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,value.ToString());
            }
           
            if (AudioManager != null) 
                AudioManager.PlayOneShot(actionParameters.CardData.AudioType);
        }
    }
}

