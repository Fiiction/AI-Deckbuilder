using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Card.CardActions
{
    public class DrawAction : CardActionBase
    {
        public override CardActionType ActionType => CardActionType.Draw;
        public override void DoAction(CardActionParameters actionParameters)
        {
            
            int value = Mathf.RoundToInt(actionParameters.Value); 
            if (CollectionManager != null)
                CollectionManager.DrawCards(Mathf.RoundToInt(value));
            else
                Debug.LogError("There is no CollectionManager");

            if (FxManager != null)
            {
                FxManager.PlayFx(actionParameters.SelfCharacter.transform, FxType.Buff);
                FxManager.SpawnFloatingText(actionParameters.TargetCharacter.TextSpawnRoot,
                    "<Color=#ffff77>Draw cards: " + value +"</color>");
            }

        }
    }
}