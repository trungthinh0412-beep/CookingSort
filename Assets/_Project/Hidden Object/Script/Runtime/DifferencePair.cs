using System;
using UnityEngine;

namespace MagicSoft.Differences
{
    // Authored data only. Found flags belong to the controller, never to the prefab.
    [Serializable]
    public sealed class DifferencePair
    {
        [SerializeField] private string id;
        [SerializeField] private DifferenceSpotView spotA;
        [SerializeField] private DifferenceSpotView spotB;

        public string Id => id;
        public DifferenceSpotView SpotA => spotA;
        public DifferenceSpotView SpotB => spotB;
    }
}
