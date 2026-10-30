using System;

namespace CounterTopRearrange
{
    /// <summary>
    /// Holds the menu's current selection so the capture pipeline
    /// (TakeScreenshot, MuseSparkVision, ObjectPlacementController) can read it
    /// without extra scene wiring. The menu sets it; it defaults to null
    /// (meaning the legacy "book" behavior) until the user picks an object.
    /// </summary>
    public static class SpawnableObjectSelection
    {
        static SpawnableObjectSO _current;

        public static event Action<SpawnableObjectSO> Changed;

        public static SpawnableObjectSO Current
        {
            get { return _current; }
            set
            {
                if (_current == value)
                    return;
                _current = value;
                if (Changed != null)
                    Changed.Invoke(_current);
            }
        }

        /// <summary>Display name of the selection, or the legacy default.</summary>
        public static string CurrentDisplayName
        {
            get
            {
                return _current != null && !string.IsNullOrWhiteSpace(_current.displayName)
                    ? _current.displayName.Trim()
                    : SpawnablePrompt.DefaultObjectName;
            }
        }
    }
}
