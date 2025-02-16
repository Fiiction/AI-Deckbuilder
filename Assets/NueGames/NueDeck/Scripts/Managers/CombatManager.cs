using System;
using System.Collections;
using System.Collections.Generic;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Characters.Enemies;
using NueGames.NueDeck.Scripts.Data.Containers;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Utils.Background;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Managers
{
    public class CombatManager : MonoBehaviour
    {
        private CombatManager(){}
        public static CombatManager Instance { get; private set; }

        [Header("References")] 
        [SerializeField] private BackgroundContainer backgroundContainer;
        [SerializeField] private List<Transform> enemyPosList;
        [SerializeField] private List<Transform> allyPosList;
 
        
        #region Cache
        public List<EnemyBase> CurrentEnemiesList { get; private set; } = new List<EnemyBase>();
        public List<AllyBase> CurrentAlliesList { get; private set; }= new List<AllyBase>();

        public Action OnAllyTurnStarted;
        public Action OnEnemyTurnStarted;
        public List<Transform> EnemyPosList => enemyPosList;

        public List<Transform> AllyPosList => allyPosList;

        public AllyBase CurrentMainAlly => CurrentAlliesList.Count>0 ? CurrentAlliesList[0] : null;

        public EnemyEncounter CurrentEncounter { get; private set; }
        
        public CombatStateType CurrentCombatStateType
        {
            get => _currentCombatStateType;
            private set
            {
                ExecuteCombatState(value);
                _currentCombatStateType = value;
            }
        }
        
        private CombatStateType _currentCombatStateType;
        protected FxManager FxManager => FxManager.Instance;
        protected AudioManager AudioManager => AudioManager.Instance;
        protected GameManager GameManager => GameManager.Instance;
        protected UIManager UIManager => UIManager.Instance;

        protected CollectionManager CollectionManager => CollectionManager.Instance;

        #endregion
        
        
        #region Setup
        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            } 
            else
            {
                Instance = this;
                CurrentCombatStateType = CombatStateType.PrepareCombat;
            }
        }

        private void Start()
        {
            StartCombat();
        }

        public void StartCombat()
        {
            BuildEnemies();
            AI_IntegrationManager.instance.SendStartGamePrompt();
            BuildAllies();
            backgroundContainer.OpenSelectedBackground();
          
            CollectionManager.SetGameDeck();
            turnIndex = 0;
            UIManager.CombatCanvas.gameObject.SetActive(true);
            UIManager.InformationCanvas.gameObject.SetActive(true);
            CurrentCombatStateType = CombatStateType.AllyTurn;
        }

        public int turnIndex = 0;
        public void AllyTurnStarted()
        {
                    
            CollectionManager.DrawCards(GameManager.PersistentGameplayData.DrawCount);

            if (CurrentMainAlly.characterStats.IsStunned)
            {
                EndTurn();
                return;
            }
            GameManager.PersistentGameplayData.CanSelectCards = true;
        }
        
        private void ExecuteCombatState(CombatStateType targetStateType)
        {
            switch (targetStateType)
            {
                case CombatStateType.PrepareCombat:
                    break;
                case CombatStateType.AllyTurn:
                    
                    turnIndex++;
                    
                    GameManager.PersistentGameplayData.CurrentMana = GameManager.PersistentGameplayData.MaxMana;
                   
                    OnAllyTurnStarted?.Invoke();
                    
                    AI_IntegrationManager.instance.StartTurnCutConversation();
                    if(turnIndex > 1)
                        AI_CardEffect.instance.AllyTurnStartEffects(CurrentMainAlly, AllyTurnStarted);
                    else
                        AllyTurnStarted();
                    break;
                case CombatStateType.EnemyTurn:

                    OnEnemyTurnStarted?.Invoke();
                    
                    CollectionManager.DiscardHand();
                    
                    StartCoroutine(nameof(EnemyTurnRoutine));
                    
                    GameManager.PersistentGameplayData.CanSelectCards = false;
                    
                    break;
                case CombatStateType.EndCombat:
                    
                    GameManager.PersistentGameplayData.CanSelectCards = false;
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetStateType), targetStateType, null);
            }
        }
        #endregion

        #region Public Methods
        public void EndTurn()
        {
            CurrentCombatStateType = CombatStateType.EnemyTurn;
        }
        public void OnAllyDeath(AllyBase targetAlly)
        {
            var targetAllyData = GameManager.PersistentGameplayData.AllyList.Find(x =>
                x.AllyCharacterData.CharacterID == targetAlly.AllyCharacterData.CharacterID);
            if (GameManager.PersistentGameplayData.AllyList.Count>1)
                GameManager.PersistentGameplayData.AllyList.Remove(targetAllyData);
            CurrentAlliesList.Remove(targetAlly);
            UIManager.InformationCanvas.ResetCanvas();
            if (CurrentAlliesList.Count<=0)
                LoseCombat();
        }
        public void OnEnemyDeath(EnemyBase targetEnemy)
        {
            CurrentEnemiesList.Remove(targetEnemy);
            if (CurrentEnemiesList.Count<=0)
                WinCombat();
        }
        public void DeactivateCardHighlights()
        {
            foreach (var currentEnemy in CurrentEnemiesList)
                currentEnemy.EnemyCanvas.SetHighlight(false);

            foreach (var currentAlly in CurrentAlliesList)
                currentAlly.AllyCanvas.SetHighlight(false);
        }
        public void IncreaseMana(int target)
        {
            GameManager.PersistentGameplayData.CurrentMana += target;
            UIManager.CombatCanvas.SetPileTexts();
        }
        public void HighlightCardTarget(bool usableWithoutTarget)
        {
            if (!usableWithoutTarget)
            {
                foreach (var currentEnemy in CurrentEnemiesList)
                    currentEnemy.EnemyCanvas.SetHighlight(true);
            }
            else
            {
                return;
            }
        }
        #endregion
        
        #region Private Methods
        private void BuildEnemies()
        {
            CurrentEncounter = GameManager.EncounterData.GetEnemyEncounter(
                GameManager.PersistentGameplayData.CurrentLevel);
            
            var enemyList = CurrentEncounter.EnemyList;
            for (var i = 0; i < enemyList.Count; i++)
            {
                var clone = Instantiate(enemyList[i].EnemyPrefab, EnemyPosList.Count >= i ? EnemyPosList[i] : EnemyPosList[0]);
                clone.BuildCharacter();
                CurrentEnemiesList.Add(clone);
            }
        }
        private void BuildAllies()
        {
            for (var i = 0; i < GameManager.PersistentGameplayData.AllyList.Count; i++)
            {
                var clone = Instantiate(GameManager.PersistentGameplayData.AllyList[i], AllyPosList.Count >= i ? AllyPosList[i] : AllyPosList[0]);
                clone.BuildCharacter();
                CurrentAlliesList.Add(clone);
            }
        }
        private void LoseCombat()
        {
            if (CurrentCombatStateType == CombatStateType.EndCombat) return;
            
            CurrentCombatStateType = CombatStateType.EndCombat;
            
            CollectionManager.DiscardHand();
            CollectionManager.DiscardPile.Clear();
            CollectionManager.DrawPile.Clear();
            CollectionManager.HandPile.Clear();
            CollectionManager.HandController.hand.Clear();
            UIManager.CombatCanvas.gameObject.SetActive(true);
            UIManager.CombatCanvas.CombatLosePanel.SetActive(true);
        }
        private void WinCombat()
        {
            if (CurrentCombatStateType == CombatStateType.EndCombat) return;
            
            AI_IntegrationManager.instance.OnEndGame();
            CurrentCombatStateType = CombatStateType.EndCombat;
           
            foreach (var allyBase in CurrentAlliesList)
            {
                GameManager.PersistentGameplayData.SetAllyHealthData(allyBase.AllyCharacterData.CharacterID,
                    allyBase.characterStats.CurrentHealth, allyBase.characterStats.MaxHealth);
            }
            
            CollectionManager.ClearPiles();
            
           
            if (GameManager.PersistentGameplayData.IsFinalEncounter)
            {
                UIManager.CombatCanvas.CombatWinPanel.SetActive(true);
            }
            else
            {
                CurrentMainAlly.characterStats.ClearAllStatus();
                GameManager.PersistentGameplayData.CurrentLevel++;
                UIManager.CombatCanvas.gameObject.SetActive(false);
                UIManager.RewardCanvas.gameObject.SetActive(true);
                UIManager.RewardCanvas.PrepareCanvas();
                UIManager.RewardCanvas.BuildRewards();
            }
           
        }
        #endregion
        
        #region Routines
        private IEnumerator EnemyTurnRoutine()
        {
            var waitDelay = new WaitForSeconds(0.6f);

            foreach (var currentEnemy in CurrentEnemiesList)
            {
                yield return currentEnemy.StartCoroutine(nameof(EnemyExample.ActionRoutine));
                yield return waitDelay;
            }

            if (CurrentCombatStateType != CombatStateType.EndCombat)
                CurrentCombatStateType = CombatStateType.AllyTurn;
        }
        #endregion
    }
}