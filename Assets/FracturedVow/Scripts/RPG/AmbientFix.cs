using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// Built games (web and desktop) start with black ambient light when a scene has no baked lighting data,
    /// because the editor works the ambient light out itself and a build doesn't. This rebuilds it from the
    /// scene's ambient colours, so the dungeon looks the same in the browser as in the editor.
    /// </summary>
    public static class AmbientFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => Rebuild();
            Rebuild();
        }

        /// <summary>Rebuilds the ambient light from RenderSettings' colours. Call after changing them.</summary>
        public static void Rebuild()
        {
            var sh = new SphericalHarmonicsL2();
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    sh.AddAmbientLight(RenderSettings.ambientLight);
                    break;
                case AmbientMode.Trilight:
                    Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor;
                    sh.AddAmbientLight(eq);
                    sh.AddDirectionalLight(Vector3.up, sky - eq, 0.5f);
                    sh.AddDirectionalLight(Vector3.down, gr - eq, 0.5f);
                    break;
                default:
                    DynamicGI.UpdateEnvironment(); // skybox lighting: let Unity work it out from the sky
                    return;
            }
            RenderSettings.ambientProbe = sh;
        }
    }
}