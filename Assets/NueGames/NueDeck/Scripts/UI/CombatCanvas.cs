using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NueGames.NueDeck.Scripts.UI
{
    public class CombatCanvas : CanvasBase
    {
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI drawPileTextField;
        [SerializeField] private TextMeshProUGUI discardPileTextField;
        [SerializeField] private TextMeshProUGUI exhaustPileTextField;
        [SerializeField] private TextMeshProUGUI manaTextTextField;
        
        [Header("Panels")]
        [SerializeField] private GameObject combatWinPanel;
        [SerializeField] private GameObject combatLosePanel;
        [SerializeField] private Button resetTurnButton;

        public TextMeshProUGUI DrawPileTextField => drawPileTextField;
        public TextMeshProUGUI DiscardPileTextField => discardPileTextField;
        public TextMeshProUGUI ManaTextTextField => manaTextTextField;
        public GameObject CombatWinPanel => combatWinPanel;
        public GameObject CombatLosePanel => combatLosePanel;

        public TextMeshProUGUI ExhaustPileTextField => exhaustPileTextField;

        #region Setup
private void Awake()
        {
            CombatWinPanel.SetActive(false);
            CombatLosePanel.SetActive(false);
            EnsureResetTurnButton();
        }
        #endregion

        #region Public Methods
        public void SetPileTexts()
        {
            DrawPileTextField.text = $"{CollectionManager.DrawPile.Count.ToString()}";
            DiscardPileTextField.text = $"{CollectionManager.DiscardPile.Count.ToString()}";
            ExhaustPileTextField.text =  $"{CollectionManager.ExhaustPile.Count.ToString()}";
            ManaTextTextField.text = $"{GameManager.PersistentGameplayData.CurrentMana.ToString()}/{GameManager.PersistentGameplayData.MaxMana}";
        }

        public override void ResetCanvas()
        {
            base.ResetCanvas();
            CombatWinPanel.SetActive(false);
            CombatLosePanel.SetActive(false);
        }

        public void EndTurn()
        {
            if (CombatManager.CurrentCombatStateType == CombatStateType.AllyTurn)
                CombatManager.EndTurn();
        }
        #endregion
    

private void EnsureResetTurnButton()
        {
            var endTurnTransform = transform.Find("EndTurnButton");
            if (endTurnTransform == null)
            {
                Debug.LogWarning("Reset Turn button could not find EndTurnButton template.");
                return;
            }

            var endTurnButton = endTurnTransform.GetComponent<Button>();
            if (endTurnButton == null)
                return;

            // resetTurnButton = Instantiate(endTurnButton, endTurnTransform.parent);
            // resetTurnButton.name = "ResetTurnButton";
            resetTurnButton.onClick = new Button.ButtonClickedEvent();
            
            var resetCanvas = resetTurnButton.gameObject.AddComponent<Canvas>();
            resetCanvas.overrideSorting = true;
            resetCanvas.sortingOrder = 1000;
            resetTurnButton.gameObject.AddComponent<GraphicRaycaster>();
            resetTurnButton.onClick.AddListener(ResetTurn);

            // var rect = resetTurnButton.GetComponent<RectTransform>();
            // float spacing = Mathf.Max(180f, rect.sizeDelta.x + 20f);
            // rect.anchoredPosition += Vector2.left * spacing;

            // var label = resetTurnButton.GetComponentInChildren<TextMeshProUGUI>(true);
            // if (label != null)
            //     label.text = "Reset Turn";

            resetTurnButton.interactable = false;
        }

        private void Update()
        {
            if (resetTurnButton != null)
                resetTurnButton.interactable = TurnResetController.Instance != null
                                               && TurnResetController.Instance.CanReset;
        }

        public void ResetTurn()
        {
            TurnResetController.Instance?.ResetTurn();
        }
}
}