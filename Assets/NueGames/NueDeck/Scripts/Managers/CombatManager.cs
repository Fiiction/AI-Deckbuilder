using System;
using AIDeckbuilder.CardRuntime;
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
                _currentCombatStateType = value;
                ExecuteCombatState(value);
            }
        }
        
        
        private bool turnTransitionInProgress;
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

            Instance = this;
            TurnResetController.Ensure(this);
            CurrentCombatStateType = CombatStateType.PrepareCombat;
        }

        private void Start()
        {
            StartCombat();
        }

        public void StartCombat()
        {
            StartCoroutine(StartCombatRoutine());
        }

        private IEnumerator StartCombatRoutine()
        {
            CardBattleEventBus.ResetBattleState();

            BuildEnemies();
            AI_IntegrationManager.instance.SendStartGamePrompt();
            BuildAllies();
            backgroundContainer.OpenSelectedBackground();

            CollectionManager.SetGameDeck();
            turnIndex = 0;
            UIManager.CombatCanvas.gameObject.SetActive(true);
            UIManager.InformationCanvas.gameObject.SetActive(true);

            yield return EnemyProgramGenerator.PrepareEncounter(CurrentEnemiesList);
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
                    StartCoroutine(AllyTurnStartRoutine());
                    break;
                case CombatStateType.EnemyTurn:
                    StartCoroutine(EnemyTurnStartRoutine());
                    break;
                case CombatStateType.EndCombat:
                    GameManager.PersistentGameplayData.CanSelectCards = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetStateType), targetStateType, null);
            }
        }

private IEnumerator AllyTurnStartRoutine()
        {
            TurnResetController.Instance?.CaptureBeforeAllyTurn();
            turnIndex++;
            turnTransitionInProgress = true;
            GameManager.PersistentGameplayData.CanSelectCards = false;
            GameManager.PersistentGameplayData.CurrentMana =
                GameManager.PersistentGameplayData.MaxMana;

            OnAllyTurnStarted?.Invoke();
            AI_IntegrationManager.instance.StartTurnCutConversation();

            yield return CardBattleEventBus.PublishRoutine(new CardBattleEventContext
            {
                Type = CardBattleEventType.TurnStart,
                ActiveCharacter = CurrentMainAlly,
                Source = CurrentMainAlly,
                Target = CurrentMainAlly
            });

            turnTransitionInProgress = false;
            AllyTurnStarted();
        }

        #endregion

        #region Public Methods
public void EndTurn()
        {
            if (turnTransitionInProgress)
                return;

            turnTransitionInProgress = true;
            GameManager.PersistentGameplayData.CanSelectCards = false;
            StartCoroutine(EndTurnRoutine());
        }

private IEnumerator EndTurnRoutine()
        {
            if (CurrentMainAlly)
            {
                yield return CardBattleEventBus.PublishRoutine(new CardBattleEventContext
                {
                    Type = CardBattleEventType.TurnEnd,
                    ActiveCharacter = CurrentMainAlly,
                    Source = CurrentMainAlly,
                    Target = CurrentMainAlly
                });
            }

            turnTransitionInProgress = false;
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

        private bool enemyTurnProcessing = false;
        private List<EnemyBase> DiePendingEnemies = new List<EnemyBase>();
        public void OnEnemyDeath(EnemyBase targetEnemy)
        {
            if (enemyTurnProcessing)
            {
                DiePendingEnemies.Add(targetEnemy);
                if(DiePendingEnemies.Count >= CurrentEnemiesList.Count)
                    WinCombat();
            }
            else
            {
                CurrentEnemiesList.Remove(targetEnemy);
                if (CurrentEnemiesList.Count<=0)
                    WinCombat();
            }
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
            CurrentEnemiesList.Clear();
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
            enemyTurnProcessing = true;
            foreach (var currentEnemy in CurrentEnemiesList.ToArray())
            {
                if (!currentEnemy || currentEnemy.dead)
                    continue;

                yield return new WaitForSeconds(CardProgramExecutor.CharacterPreActionDelaySeconds);
                yield return currentEnemy.StartCoroutine(nameof(EnemyExample.ActionRoutine));
                if (CurrentCombatStateType == CombatStateType.EndCombat)
                    break;
            }

            enemyTurnProcessing = false;
            foreach (var enemy in DiePendingEnemies)
                CurrentEnemiesList.Remove(enemy);
            DiePendingEnemies.Clear();

            if (CurrentCombatStateType != CombatStateType.EndCombat)
                CurrentCombatStateType = CombatStateType.AllyTurn;
        }
        #endregion
    

public void PrepareForTurnReset()
        {
            CardStatusRuntime.ClearInstances();

            StopAllCoroutines();
            enemyTurnProcessing = false;
            turnTransitionInProgress = false;
            DiePendingEnemies.Clear();
            foreach (var enemy in CurrentEnemiesList.ToArray())
            {
                if (!enemy || enemy.dead)
                    continue;
                CardBattleEventBus.Publish(new CardBattleEventContext
                {
                    Type = CardBattleEventType.TurnEnd,
                    ActiveCharacter = enemy,
                    Source = enemy,
                    Target = enemy
                });
            }

            OnAllyTurnStarted = null;
            OnEnemyTurnStarted = null;
            _currentCombatStateType = CombatStateType.PrepareCombat;
            DeactivateCardHighlights();
        }

        public void RestartAllyTurnFromSnapshot()
        {
            CurrentCombatStateType = CombatStateType.AllyTurn;
        }


private IEnumerator EnemyTurnStartRoutine()
        {
            turnTransitionInProgress = true;
            GameManager.PersistentGameplayData.CanSelectCards = false;
            OnEnemyTurnStarted?.Invoke();

            foreach (var enemy in CurrentEnemiesList.ToArray())
            {
                if (!enemy || enemy.dead)
                    continue;

                yield return CardBattleEventBus.PublishRoutine(new CardBattleEventContext
                {
                    Type = CardBattleEventType.TurnStart,
                    ActiveCharacter = enemy,
                    Source = enemy,
                    Target = enemy
                });
            }

            CollectionManager.DiscardHand();
            yield return EnemyTurnRoutine();
            turnTransitionInProgress = false;
        }
}
}