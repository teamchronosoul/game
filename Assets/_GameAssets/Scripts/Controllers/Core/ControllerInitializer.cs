using System.Collections.Generic;
using UnityEngine;

namespace _GameAssets.Scripts.Main
{
    public class ControllerInitializer : MonoBehaviour
    {
        [SerializeField] private Controller[] controllers;
        
        public void Awake()
        {
            //Sound.PlayMusic("mainMenu", true);
            foreach (var controller in  controllers)
            {
                controller.Initialize();
            }
        }
    }
}