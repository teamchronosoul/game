using UnityEngine;

namespace YP
{
    [System.Serializable]
    public class Product
    {
        [field: SerializeField] public string key { get; private set; }
        [field: SerializeField] public bool consumable { get; private set; }
    }
}

    
