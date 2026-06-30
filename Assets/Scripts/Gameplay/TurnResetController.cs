using System;
using AIDeckbuilder.CardRuntime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Card;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Characters;
using NueGames.NueDeck.Scripts.Data.Collection;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;
using Random = UnityEngine.Random;

public sealed class TurnResetController : MonoBehaviour
{
    public static TurnResetController Instance { get; private set; }

    private CombatManager combatManager;
    private TurnSnapshot snapshot;
    private TurnSnapshot levelSnapshot;
    private bool skipNextCapture;

    public bool IsResetting { get; private set; }
    public bool HasSnapshot => snapshot != null;
    public bool HasLevelSnapshot => levelSnapshot != null;
    public bool CanResetLevel => levelSnapshot != null
                                 && !IsResetting
                                 && combatManager != null
                                 && (combatManager.CurrentCombatStateType == CombatStateType.AllyTurn
                                     || combatManager.CurrentCombatStateType == CombatStateType.EnemyTurn);

    
public bool CanReset => snapshot != null
                            && !IsResetting
                            && combatManager != null
                            && (combatManager.CurrentCombatStateType == CombatStateType.AllyTurn
                                || combatManager.CurrentCombatStateType == CombatStateType.EnemyTurn);

    public static TurnResetController Ensure(CombatManager owner)
    {
        var controller = owner.GetComponent<TurnResetController>();
        if (controller == null)
            controller = owner.gameObject.AddComponent<TurnResetController>();

        controller.combatManager = owner;
        Instance = controller;
        return controller;
    }

    private void Awake()
    {
        Instance = this;
        if (combatManager == null)
            combatManager = GetComponent<CombatManager>();
    }

    public void CaptureBeforeAllyTurn()
    {
        if (skipNextCapture)
        {
            skipNextCapture = false;
            return;
        }

        if (IsResetting || combatManager == null || GameManager.Instance == null
                        || CollectionManager.Instance == null)
            return;

        snapshot = TurnSnapshot.Capture(combatManager);
        if (levelSnapshot == null)
            levelSnapshot = snapshot;
    }

public void ResetTurn()
    {
        if (!CanReset)
            return;

        StartCoroutine(ResetTurnCoroutine(snapshot, true));
    }

private IEnumerator ResetTurnCoroutine(TurnSnapshot targetSnapshot,
        bool restoreCardGenerationState)
    {
        IsResetting = true;
        var gameManager = GameManager.Instance;
        var collectionManager = CollectionManager.Instance;
        var handController = collectionManager.HandController;

        gameManager.PersistentGameplayData.CanUseCards = false;
        gameManager.PersistentGameplayData.CanSelectCards = false;
        handController.ResetInteractionState();

        AI_CardEffect.instance?.CancelForTurnReset();
        combatManager.PrepareForTurnReset();

        foreach (var card in FindObjectsByType<CardBase>(FindObjectsSortMode.None))
        {
            card.StopAllCoroutines();
            Destroy(card.gameObject);
        }

        foreach (var enemy in combatManager.CurrentEnemiesList.ToArray())
        {
            if (enemy == null) continue;
            enemy.StopAllCoroutines();
            Destroy(enemy.gameObject);
        }

        foreach (var ally in combatManager.CurrentAlliesList.ToArray())
        {
            if (ally == null) continue;
            ally.StopAllCoroutines();
            Destroy(ally.gameObject);
        }

        combatManager.CurrentEnemiesList.Clear();
        combatManager.CurrentAlliesList.Clear();
        handController.hand.Clear();

        foreach (var floatingText in FindObjectsByType<NueGames.NueDeck.Scripts.Utils.FloatingText>(
                     FindObjectsSortMode.None))
            Destroy(floatingText.gameObject);

        yield return new WaitForEndOfFrame();

        try
        {
            targetSnapshot.RestorePersistentData(gameManager);
            targetSnapshot.RestoreAIState(restoreCardGenerationState);

            RestorePiles(collectionManager, targetSnapshot);
            RestoreCharacters(targetSnapshot);
            RestoreHand(collectionManager, targetSnapshot);

            combatManager.turnIndex = targetSnapshot.turnIndex;
            CardBase.aiPending = false;
            Random.state = targetSnapshot.randomState;
            CardBattleEventBus.RestoreState(targetSnapshot.cardRuntime);


            UIManager.Instance.SetCanvas(UIManager.Instance.RewardCanvas, false, true);
            UIManager.Instance.SetCanvas(UIManager.Instance.CombatCanvas, true, true);
            UIManager.Instance.SetCanvas(UIManager.Instance.InformationCanvas, true, true);
            UIManager.Instance.CombatCanvas.SetPileTexts();

            handController.EnableDragging();
            skipNextCapture = true;
            IsResetting = false;
            combatManager.RestartAllyTurnFromSnapshot();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            AI_DebugCanvas.instance?.AddWarning("Reset Failed");
        }
        finally
        {
            IsResetting = false;
        }
    }

    private static void RestorePiles(CollectionManager collectionManager, TurnSnapshot state)
    {
        collectionManager.DrawPile.Clear();
        collectionManager.DrawPile.AddRange(state.drawPile);
        collectionManager.HandPile.Clear();
        collectionManager.HandPile.AddRange(state.handPile);
        collectionManager.DiscardPile.Clear();
        collectionManager.DiscardPile.AddRange(state.discardPile);
        collectionManager.ExhaustPile.Clear();
        collectionManager.ExhaustPile.AddRange(state.exhaustPile);
    }

    private void RestoreCharacters(TurnSnapshot state)
    {
        for (int i = 0; i < state.enemies.Count; i++)
        {
            var character = state.enemies[i];
            var parent = combatManager.EnemyPosList[Mathf.Min(character.slotIndex,
                combatManager.EnemyPosList.Count - 1)];
            var enemy = Instantiate(character.enemyData.EnemyPrefab, parent);
            enemy.BuildCharacter();
            enemy.UsedAbilityCount = character.enemyAbilityCount;
            combatManager.CurrentEnemiesList.Add(enemy);
            character.RestoreStats(enemy.characterStats);
        }

        for (int i = 0; i < state.allies.Count; i++)
        {
            var character = state.allies[i];
            var parent = combatManager.AllyPosList[Mathf.Min(character.slotIndex,
                combatManager.AllyPosList.Count - 1)];
            var ally = Instantiate(character.allyPrefab, parent);
            ally.BuildCharacter();
            combatManager.CurrentAlliesList.Add(ally);
            character.RestoreStats(ally.characterStats);
        }
    }

    private static void RestoreHand(CollectionManager collectionManager, TurnSnapshot state)
    {
        var gameManager = GameManager.Instance;
        foreach (var cardData in state.handPile)
        {
            var card = gameManager.BuildAndGetCard(cardData, collectionManager.HandController.drawTransform);
            collectionManager.HandController.AddCardToHand(card);
        }
    }

    private sealed class TurnSnapshot
    {
        public int turnIndex;
        public Random.State randomState;

        public int currentMana;
        public int maxMana;
        public int drawCount;
        public bool canUseCards;
        public bool canSelectCards;
        public int currentGold;
        public int currentLevel;
        public bool isFinalEncounter;
        public List<AllyBase> allyList;
        public List<CardData> currentCards;
        public List<AllyHealthState> allyHealth;

        public List<CardData> drawPile;
        public List<CardData> handPile;
        public List<CardData> discardPile;
        public List<CardData> exhaustPile;

        public List<CharacterState> allies;
        public List<CharacterState> enemies;

        public List<Message> conversation;
        public List<Message> cardConversation;
        public string pendingPrompts;
        public int levelCnt;
        public int startGameMsgCnt;
        public int startTurnMsgCnt;
        public int cardGenMessageMerged;
        public int aiTurnCnt;
        public int aiCardInTurnCnt;
        public int cardsUsedInBattle;
        public CardBattleRuntimeSnapshot cardRuntime;


        public static TurnSnapshot Capture(CombatManager combat)
        {
            var persistent = GameManager.Instance.PersistentGameplayData;
            var collection = CollectionManager.Instance;
            var ai = AI_IntegrationManager.instance;
            var cardEffect = AI_CardEffect.instance;

            return new TurnSnapshot
            {
                turnIndex = combat.turnIndex,
                randomState = Random.state,
                currentMana = persistent.CurrentMana,
                maxMana = persistent.MaxMana,
                drawCount = persistent.DrawCount,
                canUseCards = persistent.CanUseCards,
                canSelectCards = persistent.CanSelectCards,
                currentGold = persistent.CurrentGold,
                currentLevel = persistent.CurrentLevel,
                isFinalEncounter = persistent.IsFinalEncounter,
                allyList = new List<AllyBase>(persistent.AllyList),
                currentCards = new List<CardData>(persistent.CurrentCardsList),
                allyHealth = persistent.AllyHealthDataList.Select(AllyHealthState.Capture).ToList(),
                drawPile = new List<CardData>(collection.DrawPile),
                handPile = new List<CardData>(collection.HandPile),
                discardPile = new List<CardData>(collection.DiscardPile),
                exhaustPile = new List<CardData>(collection.ExhaustPile),
                allies = combat.CurrentAlliesList.Select((ally, index) =>
                    CharacterState.CaptureAlly(ally, index, persistent.AllyList)).ToList(),
                enemies = combat.CurrentEnemiesList.Select((enemy, index) =>
                    CharacterState.CaptureEnemy(enemy, index)).ToList(),
                conversation = ai == null ? new List<Message>() : new List<Message>(ai._conversationSoFar),
                cardConversation = ai == null
                    ? new List<Message>()
                    : new List<Message>(ai._cardGenConversationSoFar),
                pendingPrompts = ai == null ? "" : ai.pendingPrompts,
                levelCnt = ai == null ? 0 : ai.levelCnt,
                startGameMsgCnt = ai == null ? -1 : ai.startGameMsgCnt,
                startTurnMsgCnt = ai == null ? -1 : ai.startTurnMsgCnt,
                cardGenMessageMerged = ai == null ? -1 : ai.cardGenMessageMerged,
                aiTurnCnt = cardEffect == null ? 0 : cardEffect.turnCnt,
                aiCardInTurnCnt = cardEffect == null ? 0 : cardEffect.cardInTurnCnt,
                cardsUsedInBattle = cardEffect == null ? 0 : cardEffect.cardsUsedInBattle,
                cardRuntime = CardBattleEventBus.CaptureState()

            };
        }

        public void RestorePersistentData(GameManager gameManager)
        {
            var persistent = gameManager.PersistentGameplayData;
            persistent.CurrentMana = currentMana;
            persistent.MaxMana = maxMana;
            persistent.DrawCount = drawCount;
            persistent.CanUseCards = canUseCards;
            persistent.CanSelectCards = canSelectCards;
            persistent.CurrentGold = currentGold;
            persistent.CurrentLevel = currentLevel;
            persistent.IsFinalEncounter = isFinalEncounter;
            persistent.AllyList = new List<AllyBase>(allyList);
            persistent.CurrentCardsList = new List<CardData>(currentCards);
            persistent.AllyHealthDataList = allyHealth.Select(state => state.Restore()).ToList();
        }

        public void RestoreAIState(bool restoreCardGenerationState)
        {
            var ai = AI_IntegrationManager.instance;
            if (ai != null)
            {
                ai._conversationSoFar = new List<Message>(conversation);
                if (restoreCardGenerationState)
                    ai._cardGenConversationSoFar = new List<Message>(cardConversation);
                ai.pendingPrompts = pendingPrompts;
                ai.levelCnt = levelCnt;
                ai.startGameMsgCnt = startGameMsgCnt;
                ai.startTurnMsgCnt = startTurnMsgCnt;
                if (restoreCardGenerationState)
                    ai.cardGenMessageMerged = cardGenMessageMerged;
            }

            var cardEffect = AI_CardEffect.instance;
            if (cardEffect != null)
            {
                cardEffect.turnCnt = aiTurnCnt;
                cardEffect.cardInTurnCnt = aiCardInTurnCnt;
                cardEffect.cardsUsedInBattle = cardsUsedInBattle;
            }
        }
    }

    private sealed class CharacterState
    {
        public int slotIndex;
        public AllyBase allyPrefab;
        public EnemyCharacterData enemyData;
        public int enemyAbilityCount;
        public int currentHealth;
        public int maxHealth;
        public bool isStunned;
        public Dictionary<StatusType, StatusState> statuses;
        public Dictionary<string, int> customEffects;
        public List<RuntimeCardStatusSnapshot> runtimeStatuses;


        public static CharacterState CaptureAlly(AllyBase ally, int index, List<AllyBase> allyPrefabs)
        {
            var prefab = allyPrefabs.FirstOrDefault(item =>
                item.AllyCharacterData.CharacterID == ally.AllyCharacterData.CharacterID);
            return Capture(ally, index, prefab, null, 0);
        }

        public static CharacterState CaptureEnemy(EnemyBase enemy, int index)
        {
            return Capture(enemy, index, null, enemy.EnemyCharacterData, enemy.UsedAbilityCount);
        }

        private static CharacterState Capture(CharacterBase character, int index, AllyBase ally,
            EnemyCharacterData enemy, int abilityCount)
        {
            return new CharacterState
            {
                slotIndex = index,
                allyPrefab = ally,
                enemyData = enemy,
                enemyAbilityCount = abilityCount,
                currentHealth = character.characterStats.CurrentHealth,
                maxHealth = character.characterStats.MaxHealth,
                isStunned = character.characterStats.IsStunned,
                statuses = character.characterStats.StatusDict.ToDictionary(pair => pair.Key,
                    pair => new StatusState
                    {
                        value = pair.Value.StatusValue,
                        active = pair.Value.IsActive
                    }),
                customEffects = character.characterStats.Effects.ToDictionary(pair => pair.Key,
                    pair => pair.Value.effectValue),
                runtimeStatuses = CardStatusRuntime.Capture(character)

            };
        }

        public void RestoreStats(CharacterStats stats)
        {
            stats.MaxHealth = maxHealth;
            stats.SetCurrentHealth(currentHealth);
            stats.ClearAllStatus();

            foreach (var pair in statuses)
            {
                if (pair.Value.active)
                    stats.ApplyStatus(pair.Key, pair.Value.value);
            }

            stats.Effects.Clear();
            foreach (var pair in customEffects)
                stats.ApplyCustomEffect(pair.Key, pair.Value);

            var owner = FindOwner(stats);
            if (owner)
                CardStatusRuntime.Restore(owner, runtimeStatuses);


            stats.IsStunned = isStunned;
        }


private static CharacterBase FindOwner(CharacterStats stats)
        {
            var combat = CombatManager.Instance;
            if (combat == null)
                return null;

            foreach (var ally in combat.CurrentAlliesList)
                if (ally && ReferenceEquals(ally.characterStats, stats)) return ally;
            foreach (var enemy in combat.CurrentEnemiesList)
                if (enemy && ReferenceEquals(enemy.characterStats, stats)) return enemy;
            return null;
        }
}

    private sealed class StatusState
    {
        public int value;
        public bool active;
    }

    private sealed class AllyHealthState
    {
        public string id;
        public int currentHealth;
        public int maxHealth;

        public static AllyHealthState Capture(AllyHealthData data)
        {
            return new AllyHealthState
            {
                id = data.CharacterId,
                currentHealth = data.CurrentHealth,
                maxHealth = data.MaxHealth
            };
        }

        public AllyHealthData Restore()
        {
            return new AllyHealthData
            {
                CharacterId = id,
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth
            };
        }
    }


public void ResetLevel()
    {
        if (!CanResetLevel)
            return;

        snapshot = levelSnapshot;
        StartCoroutine(ResetTurnCoroutine(levelSnapshot, false));
    }
}
