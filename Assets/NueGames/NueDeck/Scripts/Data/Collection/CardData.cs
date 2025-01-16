using System;
using System.Collections.Generic;
using System.Text;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using NueGames.NueDeck.Scripts.NueExtentions;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Data.Collection
{
    [CreateAssetMenu(fileName = "Card Data", menuName = "NueDeck/Collection/Card", order = 0)]
    public class CardData : ScriptableObject
    {
        [Header("Card Profile")] [SerializeField]
        private string id;

        [SerializeField] private string cardName;
        [SerializeField] private string description;
        [SerializeField] private string imagePrompt;

        [SerializeField] private int manaCost;
        [SerializeField] private Sprite cardSprite;
        [SerializeField] private RarityType rarity;

        [Header("Action Settings")] [SerializeField]
        private bool usableWithoutTarget;

        [SerializeField] private bool exhaustAfterPlay;
        // [SerializeField] private List<CardActionData> cardActionDataList;

        // [Header("Description")]
        // [SerializeField] private List<CardDescriptionData> cardDescriptionDataList;
        // [SerializeField] private List<SpecialKeywords> specialKeywordsList;

        [Header("Fx")] [SerializeField] private AudioActionType audioType;

        #region Cache

        public string Id => id;
        public bool UsableWithoutTarget => usableWithoutTarget;
        public int ManaCost => manaCost;
        public string CardName => cardName;
        public string CardDescription => description;

        public Sprite CardSprite => cardSprite;

        public AudioActionType AudioType => audioType;
        public string MyDescription { get; set; }
        public RarityType Rarity => rarity;

        public bool ExhaustAfterPlay => exhaustAfterPlay;

        #endregion

        #region Methods

        public void UpdateDescription()
        {
            MyDescription = description;
        }

        #endregion

        private static int generationCnt = 0;
        public CardData(string _name, string _desc, int _manaCost,
            bool _needTarget, RarityType _rarity = RarityType.Common)
        {
            cardName = _name;
            id = "AI_" + (generationCnt++).ToString() +"_" + _name;
            description = _desc;
            manaCost = _manaCost;
            usableWithoutTarget = !_needTarget;
            rarity = _rarity;
            
        }

        public void SetCardSprite(Sprite _sprite)
        {
            cardSprite = _sprite;
        }

    }

    [Serializable]
    public class CardActionData
    {
        [SerializeField] private CardActionType cardActionType;
        [SerializeField] private ActionTargetType actionTargetType;
        [SerializeField] private float actionValue;
        [SerializeField] private float actionDelay;
        [SerializeField] private string strParameter;

        public ActionTargetType ActionTargetType => actionTargetType;
        public CardActionType CardActionType => cardActionType;
        public float ActionValue => actionValue;
        public float ActionDelay => actionDelay;

        public string StrParameter => strParameter;

        public CardActionData()
        {
        }

        public CardActionData(CardActionType _cardActionType, ActionTargetType _actionTargetType, float _actionValue,
            string _strParameter)
        {
            cardActionType = _cardActionType;
            actionTargetType = _actionTargetType;
            actionValue = _actionValue;
            strParameter = _strParameter;
            actionDelay = 0f;
        }

        #region Editor

#if UNITY_EDITOR
        public void EditActionType(CardActionType newType) => cardActionType = newType;
        public void EditActionTarget(ActionTargetType newTargetType) => actionTargetType = newTargetType;
        public void EditActionValue(float newValue) => actionValue = newValue;
        public void EditActionDelay(float newValue) => actionDelay = newValue;

#endif


        #endregion
    }
}
