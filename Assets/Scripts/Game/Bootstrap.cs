using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// Spawns the game in whatever scene is open, so pressing Play in any scene starts a new
    /// school: the game controller, the campus view and interaction, the people walking around,
    /// the notebook HUD (with the debug UI behind F1 in development builds), the music and sounds,
    /// and a campus camera. Add a GameController to a scene to configure it there instead.
    /// </summary>
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;

            var go = new GameObject("SchoolSim");
            go.AddComponent<GameController>();
            go.AddComponent<CampusView>();
            go.AddComponent<CampusController>();
            go.AddComponent<CampusPeople>();
            go.AddComponent<DebugUI>();
            go.AddComponent<GameHud>();
            go.AddComponent<GameAudio>();
            if (UiTour.RequestedFolder() != null) go.AddComponent<UiTour>();
            if (ReleaseChecks.Requested()) go.AddComponent<ReleaseChecks>();
            Object.DontDestroyOnLoad(go);

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            if (cam.GetComponent<CampusCamera>() == null) cam.gameObject.AddComponent<CampusCamera>();
        }
    }
}
