using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Characters.Allies
{
    public class PlayerExample : AllyBase
    {
        public override void BuildCharacter()
        {
            base.BuildCharacter();
            if (UIManager != null)
                characterStats.OnHealthChanged += UIManager.InformationCanvas.SetHealthText;
            characterStats.SetCurrentHealth(characterStats.CurrentHealth);

            if (AI_ImageGeneration.instance.heroSpriteSet)
            {
                transform.Find("SpriteParent").GetComponentInChildren<SpriteRenderer>().sprite
                    = AI_ImageGeneration.instance.heroSprite;
            }
        }
    }
}