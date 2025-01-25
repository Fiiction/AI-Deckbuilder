using System.Collections.Generic;
using NueGames.NueDeck.Scripts.Enums;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Data.Collection.RewardData
{
    [CreateAssetMenu(fileName = "Card Reward Data",menuName = "NueDeck/Collection/Rewards/CardRW",order = 0)]
    public class CardRewardData : RewardDataBase
    {
        [SerializeField] private List<CardData> rewardCardList;
        public List<CardData> RewardCardList => rewardCardList;

        public CardRewardData(RarityType rarity, int cnt)
        {
            rewardCardList = new List<CardData>();
            switch (rarity)
            {
                case RarityType.Common:
                    return;
                case RarityType.Rare:
                    rewardCardList.Add(AI_DeckGenerator.instance.rareCards[cnt]);
                    return;
                case RarityType.Epic:
                    rewardCardList.Add(AI_DeckGenerator.instance.epicCards[cnt]);
                    return;
                case RarityType.Legendary:
                    rewardCardList.Add(AI_DeckGenerator.instance.legendCards[cnt]);
                    return;
                default:
                    return;
            }
        }
        
    }
}