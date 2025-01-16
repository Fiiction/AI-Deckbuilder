using NueGames.NueDeck.Scripts.Data.Collection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NueGames.NueDeck.Scripts.UI.Reward
{
    public class RewardContainer : MonoBehaviour
    {
        [SerializeField] private Button rewardButton;
        [SerializeField] private Image rewardImage;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] public CardData cardData;
        
        public Button RewardButton => rewardButton;

        public void BuildReward(CardData _cardData)
        {
            rewardImage.sprite = _cardData.CardSprite;
            rewardText.text = _cardData.CardDescription;
            cardData = _cardData;
        }
        
    }
}