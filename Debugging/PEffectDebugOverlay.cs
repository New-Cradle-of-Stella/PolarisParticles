using UnityEngine;

namespace Polaris.Particles.Debugging
{
    internal sealed class PEffectDebugOverlay : MonoBehaviour
    {
        private void Update() => PEffectDebugPage.Tick();

        private void OnGUI() => PEffectDebugPage.Draw();
    }
}
