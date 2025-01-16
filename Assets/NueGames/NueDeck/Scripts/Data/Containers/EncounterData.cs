using System;
using System.Collections.Generic;
using System.Linq;
using NueGames.NueDeck.Scripts.Characters;
using NueGames.NueDeck.Scripts.Data.Characters;
using NueGames.NueDeck.Scripts.Enums;
using NueGames.NueDeck.Scripts.NueExtentions;
using UnityEngine;

namespace NueGames.NueDeck.Scripts.Data.Containers
{
    [CreateAssetMenu(fileName = "Encounter Data", menuName = "NueDeck/Containers/EncounterData", order = 4)]
    public class EncounterData : ScriptableObject
    {
        [Header("Settings")] 
        //[SerializeField] private bool encounterRandomlyAtStage;
        [SerializeField] private List<EnemyEncounter> enemyEncounterList;

        //public bool EncounterRandomlyAtStage => encounterRandomlyAtStage;
        public List<EnemyEncounter> EnemyEncounterList => enemyEncounterList;

        public EnemyEncounter GetEnemyEncounter(int encounterId =0)
        {
            if(encounterId<0 || encounterId>=EnemyEncounterList.Count)
                return null;
            return EnemyEncounterList[encounterId];
        }
        
    }

    [Serializable]
    public class EnemyEncounter : EncounterBase
    {
        [SerializeField] private List<EnemyCharacterData> enemyList;
        public List<EnemyCharacterData> EnemyList => enemyList;
    }
    
    [Serializable]
    public abstract class EncounterBase
    {
        [SerializeField] private BackgroundTypes targetBackgroundType;

        public BackgroundTypes TargetBackgroundType => targetBackgroundType;
    }
}