using System;
using System.Collections.Generic;
using NueGames.NueDeck.Scripts.Card;
using NueGames.NueDeck.Scripts.Data.Collection;
using NueGames.NueDeck.Scripts.Data.Containers;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.NueExtentions;
using UnityEngine;
using NueGames.NueDeck.Scripts.Managers;

namespace NueGames.NueDeck.Scripts.UI.Reward
{
    public class RewardCanvas : CanvasBase
    {
        [Header("References")]
        [SerializeField] private RewardContainerData rewardContainerData;
        [SerializeField] private Transform rewardRoot;
        [SerializeField] private RewardContainer rewardContainerPrefab;
        [SerializeField] private Transform rewardPanelRoot;
        [Header("Choice")]
        [SerializeField] private Transform choice2DCardSpawnRoot;
        [SerializeField] private ChoiceCard choiceCardUIPrefab;
        [SerializeField] private ChoicePanel choicePanel;
        
        private readonly List<RewardContainer> _currentRewardsList = new List<RewardContainer>();
        private readonly List<ChoiceCard> _spawnedChoiceList = new List<ChoiceCard>();
        private readonly List<CardData> _cardRewardList = new List<CardData>();

        public ChoicePanel ChoicePanel => choicePanel;
        
        #region Public Methods

        public void PrepareCanvas()
        {
            rewardPanelRoot.gameObject.SetActive(true);
        }

        public void BuildRewards()
        {
            int level = GameManager.PersistentGameplayData.CurrentLevel;
            Debug.Log("Build Rewards: Level " + level);
            if (level <= 1)
            {
                BuildSingleCardReward(AI_DeckGenerator.instance.rareCards[0]);
                BuildSingleCardReward(AI_DeckGenerator.instance.rareCards[1]);
                BuildSingleCardReward(AI_DeckGenerator.instance.rareCards[2]);
            }
            else if (level == 2)
            {
                BuildSingleCardReward(AI_DeckGenerator.instance.rareCards[3]);
                BuildSingleCardReward(AI_DeckGenerator.instance.rareCards[4]);
                BuildSingleCardReward(AI_DeckGenerator.instance.rareCards[5]);
            }
        }

        void BuildSingleCardReward(CardData cardData)
        {
            var rewardClone = Instantiate(rewardContainerPrefab, rewardRoot);
            _currentRewardsList.Add(rewardClone);
            _cardRewardList.Add(cardData);
            rewardClone.BuildReward(cardData);
            rewardClone.RewardButton.onClick.AddListener(()=>GetCardReward(rewardClone,3));
        }
        
        public override void ResetCanvas()
        {
            ResetRewards();

            ResetChoice();
        }

        private void ResetRewards()
        {
            foreach (var rewardContainer in _currentRewardsList)
                Destroy(rewardContainer.gameObject);
        
            _currentRewardsList?.Clear();
        }

        private void ResetChoice()
        {
            foreach (var choice in _spawnedChoiceList)
            {
                Destroy(choice.gameObject);
            }

            _spawnedChoiceList?.Clear();
            ChoicePanel.DisablePanel();
        }

        #endregion
        
        #region Private Methods

        private void GetCardReward(RewardContainer rewardContainer,int amount = 3)
        {
            for(int i = 0; i < amount; i++)
                GameManager.Instance.AddCardToDeck(rewardContainer.cardData);
            _currentRewardsList.Remove(rewardContainer);
            Destroy(rewardContainer.gameObject);
        }
        #endregion
        
    }
}