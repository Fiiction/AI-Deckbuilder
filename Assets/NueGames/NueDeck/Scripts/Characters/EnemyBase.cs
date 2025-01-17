using System.Collections;
using NueGames.NueDeck.Scripts.Data.Characters;
using NueGames.NueDeck.Scripts.Data.Containers;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.Interfaces;
using NueGames.NueDeck.Scripts.Managers;
using NueGames.NueDeck.Scripts.NueExtentions;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Characters
{
    public class EnemyBase : CharacterBase, IEnemy
    {
        [Header("Enemy Base References")]
        [SerializeField] protected EnemyCharacterData enemyCharacterData;
        [SerializeField] protected EnemyCanvas enemyCanvas;
        [SerializeField] protected SoundProfileData deathSoundProfileData;
        public EnemyAbilityData NextAbility;
        
        public EnemyCharacterData EnemyCharacterData => enemyCharacterData;
        public EnemyCanvas EnemyCanvas => enemyCanvas;
        public SoundProfileData DeathSoundProfileData => deathSoundProfileData;

        #region Setup
        public override void BuildCharacter()
        {
            base.BuildCharacter();
            EnemyCanvas.InitCanvas();
            characterStats = new CharacterStats(EnemyCharacterData.MaxHealth,EnemyCanvas);
            characterStats.OnDeath += OnDeath;
            characterStats.SetCurrentHealth(characterStats.CurrentHealth);
            CombatManager.OnAllyTurnStarted += ShowNextAbility;
            CombatManager.OnEnemyTurnStarted += characterStats.TriggerAllStatus;
        }
        protected override void OnDeath()
        {
            base.OnDeath();
            CombatManager.OnAllyTurnStarted -= ShowNextAbility;
            CombatManager.OnEnemyTurnStarted -= characterStats.TriggerAllStatus;
           
            CombatManager.OnEnemyDeath(this);
            AudioManager.PlayOneShot(DeathSoundProfileData.GetRandomClip());
            Destroy(gameObject);
        }
        #endregion
        
        #region Private Methods

        private int _usedAbilityCount;
        private void ShowNextAbility()
        {
            NextAbility = EnemyCharacterData.GetAbility(_usedAbilityCount);
            EnemyCanvas.IntentImage.sprite = NextAbility.Intention.IntentionSprite;
            
            EnemyCanvas.NextActionValueText.gameObject.SetActive(true);

            _usedAbilityCount++;
            EnemyCanvas.IntentImage.gameObject.SetActive(true);
        }
        #endregion
        
        #region Action Routines
        public virtual IEnumerator ActionRoutine()
        {
            if (characterStats.IsStunned)
                yield break;
            
            EnemyCanvas.IntentImage.gameObject.SetActive(false);
            
            bool waiting = true;
            AI_CardEffect.instance.EnemyTurn(this,
                NextAbility.Name, NextAbility.Desc, ()=>{waiting = false; });
            yield return StartCoroutine(GoCoroutine());
            yield return new WaitWhile(() => waiting);
            yield return StartCoroutine(BackCoroutine());
        }

        private Vector3 startPos;
        private Vector3 endPos;
        private Quaternion startRot;
        Quaternion endRot;
        IEnumerator GoCoroutine()
        {
            var waitFrame = new WaitForEndOfFrame();
            
            startPos = transform.position;
            endPos = startPos+new Vector3(-0.4f,0.2f,0);
            
            startRot = transform.localRotation;
            endRot = transform.localRotation;
            
            return MoveToTargetRoutine(waitFrame, startPos, endPos, startRot, endRot, 5);
            
        }
        
        IEnumerator BackCoroutine()
        {
            var waitFrame = new WaitForEndOfFrame();
            
            return MoveToTargetRoutine(waitFrame, endPos, startPos, endRot, startRot, 5);

        }
        // protected virtual IEnumerator BuffRoutine(EnemyAbilityData targetAbility)
        // {
        //     var waitFrame = new WaitForEndOfFrame();
        //     
        //     var target = CombatManager.CurrentEnemiesList.RandomItem();
        //     
        //     var startPos = transform.position;
        //     var endPos = startPos+new Vector3(0,0.2f,0);
        //     
        //     var startRot = transform.localRotation;
        //     var endRot = transform.localRotation;
        //     
        //     yield return StartCoroutine(MoveToTargetRoutine(waitFrame, startPos, endPos, startRot, endRot, 5));
        //     
        //     targetAbility.ActionList.ForEach(x=>EnemyActionProcessor.GetAction(x.ActionType).DoAction(new EnemyActionParameters(x.ActionValue,target,this)));
        //     
        //     yield return StartCoroutine(MoveToTargetRoutine(waitFrame, endPos, startPos, endRot, startRot, 5));
        // }
        #endregion
        
        #region Other Routines
        private IEnumerator MoveToTargetRoutine(WaitForEndOfFrame waitFrame,Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float speed)
        {
            var timer = 0f;
            while (true)
            {
                timer += Time.deltaTime*speed;

                transform.position = Vector3.Lerp(startPos, endPos, timer);
                transform.localRotation = Quaternion.Lerp(startRot,endRot,timer);
                if (timer>=1f)
                {
                    break;
                }

                yield return waitFrame;
            }
        }

        #endregion
    }
}