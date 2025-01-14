using NueGames.NueDeck.Scripts.Managers;

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
        }
    }
}