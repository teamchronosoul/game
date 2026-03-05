using TMPro;
using UnityEngine;

namespace YP.Internal
{
    public class VersionLabel : MonoBehaviour
    {
        private void OnEnable() 
            => GetComponent<TextMeshProUGUI>().text = Core.version;
    }
}


