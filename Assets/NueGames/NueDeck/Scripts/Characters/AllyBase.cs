using System;
using NueGames.NueDeck.Scripts.Data.Characters;
using NueGames.NueDeck.Scripts.Interfaces;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Characters
{
    public abstract class AllyBase : CharacterBase,IAlly
    {
        [Header("Ally Base Settings")]
        [SerializeField] private AllyCanvas allyCanvas;
        [SerializeField] private AllyCharacterData allyCharacterData;
        public AllyCanvas AllyCanvas => allyCanvas;
        public AllyCharacterData AllyCharacterData => allyCharacterData;
        
        public override void BuildCharacter()
        {
            base.BuildCharacter();
            allyCanvas.InitCanvas();
            characterStats = new CharacterStats(allyCharacterData.MaxHealth,allyCanvas);

            if (!GameManager)
                throw new Exception("There is no GameManager");
            
            var data = GameManager.PersistentGameplayData.AllyHealthDataList.Find(x =>
                x.CharacterId == AllyCharacterData.CharacterID);
            
            if (data != null)
            {
                characterStats.CurrentHealth = data.CurrentHealth;
                characterStats.MaxHealth = data.MaxHealth;
            }
            else
            {
                GameManager.PersistentGameplayData.SetAllyHealthData(AllyCharacterData.CharacterID,characterStats.CurrentHealth,characterStats.MaxHealth);
            }
            
            characterStats.OnDeath += OnDeath;
            characterStats.SetCurrentHealth(characterStats.CurrentHealth);
            
            if (CombatManager != null)
                CombatManager.OnAllyTurnStarted += characterStats.TriggerAllStatus;
        }
        
        protected override void OnDeath()
        {
            base.OnDeath();
            if (CombatManager != null)
            {
                CombatManager.OnAllyTurnStarted -= characterStats.TriggerAllStatus;
                CombatManager.OnAllyDeath(this);
            }

            Destroy(gameObject);
        }
    }

    [Serializable]
    public class AllyHealthData
    {
        [SerializeField] private string characterId;
        [SerializeField] private int maxHealth;
        [SerializeField] private int currentHealth;
        
        public int MaxHealth
        {
            get => maxHealth;
            set => maxHealth = value;
        }

        public int CurrentHealth
        {
            get => currentHealth;
            set => currentHealth = value;
        }

        public string CharacterId
        {
            get => characterId;
            set => characterId = value;
        }
    }
}