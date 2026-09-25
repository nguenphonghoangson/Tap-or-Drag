using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Spawns the game into whatever scene is open, so pressing Play is enough.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<GameManager>() != null) return;
            new GameObject("TapOrDrag").AddComponent<GameManager>();
        }
    }
}
