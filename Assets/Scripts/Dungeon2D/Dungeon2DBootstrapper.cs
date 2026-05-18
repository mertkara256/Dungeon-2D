using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon2D
{
    public static class Dungeon2DBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureGame();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureGame();
        }

        private static void EnsureGame()
        {
            if (Object.FindFirstObjectByType<Dungeon2DGame>() != null)
            {
                return;
            }

            GameObject host = new GameObject(Dungeon2DConfig.RuntimeRootName);
            host.AddComponent<Dungeon2DGame>();
        }
    }
}
