using System;
using System.Collections;
using NueGames.NueDeck.Scripts.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NueGames.NueDeck.Scripts.Utils
{
    [DefaultExecutionOrder(-11)]
    public class CoreLoader : MonoBehaviour
    {
        public GameObject managerRootPrefab;
        public static GameObject managerRootInstance = null;

        IEnumerator ResetCoroutine()
        {
            yield return 0;
            managerRootInstance = Instantiate(managerRootPrefab);
            DontDestroyOnLoad(managerRootInstance);
        }
        
        private void Awake()
        {
            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                Destroy(gameObject);
                return;
            }
            if (managerRootInstance != null)
            {
                Destroy(managerRootInstance);
                managerRootInstance = null;
            }
            
            StartCoroutine(ResetCoroutine());
           
        }
    }
}