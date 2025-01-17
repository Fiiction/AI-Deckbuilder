using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class EarnManaAction : CardActionBase
    {
        public override CardActionType ActionType => CardActionType.EarnMana;
        public override void DoAction(CardActionParameters actionParameters)
        {
            int value = Mathf.RoundToInt(actionParameters.Value); 
            if (CombatManager != null)
                CombatManager.IncreaseMana(Mathf.RoundToInt(value));
            else
                Debug.LogError("There is no CombatManager");

            if (FxManager != null)
            {
                FxManager.PlayFx(actionParameters.SelfCharacter.transform, FxType.Buff);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,
                    "<Color=#4499ee>+Mana: " + value +"</color>");
            }
            
        }
    }
}