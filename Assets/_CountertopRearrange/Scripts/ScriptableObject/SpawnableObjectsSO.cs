using System.Collections.Generic;
using UnityEngine;

namespace CounterTopRearrange
{
    /// <summary>
    /// Catalog of spawnable objects shown in the horizontal scroll menu.
    /// Created via Assets > Create > Countertop > Spawnable Objects.
    /// </summary>
    [CreateAssetMenu(menuName = "Countertop/Spawnable Objects", fileName = "SpawnableObjectsSO")]
    public class SpawnableObjectsSO : ScriptableObject
    {
        [Tooltip("Objects shown in the horizontal scroll menu, in order.")]
        public List<SpawnableObjectSO> objects = new List<SpawnableObjectSO>();
    }
}
