using UnityEngine;

namespace CounterTopRearrange
{
    /// <summary>
    /// One spawnable entry: display name, menu image and the prefab to place.
    /// Created via Assets > Create > Countertop > Spawnable Object.
    /// </summary>
    [CreateAssetMenu(menuName = "Countertop/Spawnable Object", fileName = "SpawnableObjectSO")]
    public class SpawnableObjectSO : ScriptableObject
    {
        [Tooltip("Name shown in the menu and sent to Muse, e.g. \"red cube\".")]
        public string displayName;

        [Tooltip("Image shown on the horizontal scroll menu. When empty, the menu draws a fallback icon from the prefab.")]
        public Sprite menuImage;

        [Tooltip("Prefab spawned on the counter at the chosen spot.")]
        public GameObject prefab;
    }
}
