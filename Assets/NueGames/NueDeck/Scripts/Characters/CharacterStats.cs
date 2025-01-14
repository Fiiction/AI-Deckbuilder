using System;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Enums;

namespace NueGames.NueDeck.Scripts.Characters
{
    public class CustomEffects
    {
        string effectName;
        public int effectValue;
        public bool isNegative;

        public CustomEffects(string _effectName, int _effectValue, bool _isNegative)
        {
            effectName = _effectName;
            effectValue = _effectValue;
            isNegative = _isNegative;
        }
    }
    
    
    public class StatusStats
    { 
        public StatusType StatusType { get; set; }
        public int StatusValue { get; set; }
        public bool DecreaseOverTurn { get; set; } // If true, decrease on turn end
        public bool IsPermanent { get; set; } // If true, status can not be cleared during combat
        public bool IsActive { get; set; }
        public bool CanNegativeStack { get; set; }
        public bool ClearAtNextTurn { get; set; }
        
        public Action OnTriggerAction;
        public StatusStats(StatusType statusType,int statusValue,bool decreaseOverTurn = false, bool isPermanent = false,bool isActive = false,bool canNegativeStack = false,bool clearAtNextTurn = false)
        {
            StatusType = statusType;
            StatusValue = statusValue;
            DecreaseOverTurn = decreaseOverTurn;
            IsPermanent = isPermanent;
            IsActive = isActive;
            CanNegativeStack = canNegativeStack;
            ClearAtNextTurn = clearAtNextTurn;
        }
    }
    [Serializable]
    public class CharacterStats
    { 
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public bool IsStunned { get;  set; }
        public bool IsDeath { get; private set; }
       
        public Action OnDeath;
        public Action<int, int> OnHealthChanged;
        private readonly Action<StatusType,int> OnStatusChanged;
        private readonly Action<StatusType, int> OnStatusApplied;
        private readonly Action<StatusType> OnStatusCleared;
        private readonly Action<Dictionary<string, CustomEffects>> OnCustomEffectsChanged;
        public Action OnHealAction;
        public Action OnTakeDamageAction;
        public Action OnShieldGained;
        
        public Dictionary<StatusType, StatusStats> StatusDict = new Dictionary<StatusType, StatusStats>();
        public Dictionary<string, CustomEffects> Effects = new ();
        
        #region Setup
        public CharacterStats(int maxHealth, CharacterCanvas characterCanvas)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            SetAllStatus();
            
            OnHealthChanged += characterCanvas.UpdateHealthText;
            OnStatusChanged += characterCanvas.UpdateStatusText;
            OnStatusApplied += characterCanvas.ApplyStatus;
            OnStatusCleared += characterCanvas.ClearStatus;
            OnCustomEffectsChanged += characterCanvas.UpdateCustomEffects;
        }
        
        private void SetAllStatus()
        {
            for (int i = 0; i < Enum.GetNames(typeof(StatusType)).Length; i++)
                StatusDict.Add((StatusType) i, new StatusStats((StatusType) i, 0));

            StatusDict[StatusType.Poison].DecreaseOverTurn = true;
            StatusDict[StatusType.Poison].OnTriggerAction += DamagePoison;

            StatusDict[StatusType.Block].ClearAtNextTurn = true;

            StatusDict[StatusType.Strength].CanNegativeStack = true;
            StatusDict[StatusType.Dexterity].CanNegativeStack = true;
            
            StatusDict[StatusType.Stun].DecreaseOverTurn = true;
            StatusDict[StatusType.Stun].OnTriggerAction += CheckStunStatus;
            
        }
        #endregion
        
        #region Public Methods
        public void ApplyStatus(StatusType targetStatus,int value)
        {
            if (StatusDict[targetStatus].IsActive)
            {
                StatusDict[targetStatus].StatusValue += value;
                OnStatusChanged?.Invoke(targetStatus, StatusDict[targetStatus].StatusValue);
                
            }
            else
            {
                StatusDict[targetStatus].StatusValue = value;
                StatusDict[targetStatus].IsActive = true;
                OnStatusApplied?.Invoke(targetStatus, StatusDict[targetStatus].StatusValue);
            }
        }

        public void ApplyCustomEffect(string effectName, int value)
        {
            if(Effects.ContainsKey(effectName))
                Effects[effectName].effectValue += value;
            else
                Effects.Add(effectName, new CustomEffects(effectName, value, false));
            
            OnCustomEffectsChanged?.Invoke(Effects);
        }
        
        public void TriggerAllStatus()
        {
            for (int i = 0; i < Enum.GetNames(typeof(StatusType)).Length; i++)
                TriggerStatus((StatusType) i);
            
            // Process all custom effects
            var removeEffectList = new List<string>();
            foreach (var i in Effects)
            {
                i.Value.effectValue--;
                if(i.Value.effectValue <= 0)
                    removeEffectList.Add(i.Key);
            }
            foreach (var i in removeEffectList)
                Effects.Remove(i);
            OnCustomEffectsChanged?.Invoke(Effects);
        }
        
        public void SetCurrentHealth(int targetCurrentHealth)
        {
            CurrentHealth = targetCurrentHealth <=0 ? 1 : targetCurrentHealth;
            OnHealthChanged?.Invoke(CurrentHealth,MaxHealth);
        } 
        
        public void Heal(int value)
        {
            CurrentHealth += value;
            if (CurrentHealth>MaxHealth)  CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth,MaxHealth);
        }
        
        public void Damage(int value, bool canPierceArmor = false)
        {
            if (IsDeath) return;
            OnTakeDamageAction?.Invoke();
            var remainingDamage = value;
            
            if (!canPierceArmor)
            {
                if (StatusDict[StatusType.Block].IsActive)
                {
                    ApplyStatus(StatusType.Block,-value);

                    remainingDamage = 0;
                    if (StatusDict[StatusType.Block].StatusValue <= 0)
                    {
                        remainingDamage = StatusDict[StatusType.Block].StatusValue * -1;
                        ClearStatus(StatusType.Block);
                    }
                }
            }
            
            CurrentHealth -= remainingDamage;
            
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                OnDeath?.Invoke();
                IsDeath = true;
            }
            OnHealthChanged?.Invoke(CurrentHealth,MaxHealth);
        }
        
        public void IncreaseMaxHealth(int value)
        {
            MaxHealth += value;
            OnHealthChanged?.Invoke(CurrentHealth,MaxHealth);
        }

        public void ClearAllStatus()
        {
            foreach (var status in StatusDict)
                ClearStatus(status.Key);
        }
           
        public void ClearStatus(StatusType targetStatus)
        {
            StatusDict[targetStatus].IsActive = false;
            StatusDict[targetStatus].StatusValue = 0;
            OnStatusCleared?.Invoke(targetStatus);
        }

        #endregion

        #region Private Methods
        private void TriggerStatus(StatusType targetStatus)
        {
            StatusDict[targetStatus].OnTriggerAction?.Invoke();
            
            //One turn only statuses
            if (StatusDict[targetStatus].ClearAtNextTurn)
            {
                ClearStatus(targetStatus);
                OnStatusChanged?.Invoke(targetStatus, StatusDict[targetStatus].StatusValue);
                return;
            }
            
            //Check status
            if (StatusDict[targetStatus].StatusValue <= 0)
            {
                if (StatusDict[targetStatus].CanNegativeStack)
                {
                    if (StatusDict[targetStatus].StatusValue == 0 && !StatusDict[targetStatus].IsPermanent)
                        ClearStatus(targetStatus);
                }
                else
                {
                    if (!StatusDict[targetStatus].IsPermanent)
                        ClearStatus(targetStatus);
                }
            }
            
            if (StatusDict[targetStatus].DecreaseOverTurn) 
                StatusDict[targetStatus].StatusValue--;
            
            if (StatusDict[targetStatus].StatusValue == 0)
                if (!StatusDict[targetStatus].IsPermanent)
                    ClearStatus(targetStatus);
            
            OnStatusChanged?.Invoke(targetStatus, StatusDict[targetStatus].StatusValue);
        }
        
     
        private void DamagePoison()
        {
            if (StatusDict[StatusType.Poison].StatusValue<=0) return;
            Damage(StatusDict[StatusType.Poison].StatusValue,true);
        }
        
        public void CheckStunStatus()
        {
            if (StatusDict[StatusType.Stun].StatusValue <= 0)
            {
                IsStunned = false;
                return;
            }
            
            IsStunned = true;
        }
        
        #endregion
    }
}