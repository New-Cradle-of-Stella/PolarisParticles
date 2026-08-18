using System;
using UnityEngine;
using XX;

namespace Polaris.Particles.Debugging
{
    /// <summary>
    /// 使用独立 Effect、Camera 与 RenderTexture 承载预览。预览场景放在远离地图的位置，
    /// 只对专用相机可见，最终纹理由 IMGUI 页面绘制。
    /// </summary>
    internal sealed class PEffectPreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private const int TextureWidth = 960;
        private const int TextureHeight = 400;
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 4f;

        /// <summary>1× 缩放时的正交半高。</summary>
        private const float BaseOrthographicSize = 3.125f;

        private static readonly Vector3 PreviewOrigin = new Vector3(10000f, 10000f, 0f);

        private GameObject _scene;
        private Camera _camera;
        private RenderTexture _texture;
        private Effect<EffectItem> _effect;
        private PTCThread _timeline;
        private float _zoom = 1f;

        internal static PEffectPreviewRenderer Instance { get; private set; }
        internal Texture Texture => _texture;
        internal bool IsPlaying => _effect != null && _effect.isActive();

        private void Awake()
        {
            Instance = this;
            CreatePreviewScene();
        }

        private void Update()
        {
            if (_effect == null)
                return;

            try
            {
                _effect.runDraw(1f);
            }
            catch (Exception ex)
            {
                Stop();
                PolarisAPI.Errors.Report(ex, "rendering isolated .peffect preview", typeof(PEffectPreviewRenderer).Assembly);
            }
        }

        internal void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (_camera != null)
                _camera.orthographicSize = BaseOrthographicSize / _zoom;
        }

        internal void SetVisible(bool visible)
        {
            if (_camera != null)
                _camera.enabled = visible;
            if (!visible)
                Stop();
        }

        internal EffectItem PlayParticle(string key, float x, float y, float z, int time)
        {
            Stop();
            EfParticle source = EfParticleManager.Get(key, false, true);
            if (source == null)
                return null;

            // 调试页的 Life 字段应始终有效，不要求用户在模板里额外声明 `time maxt`。
            EfParticle preview = source.clone();
            preview.rep_time = "maxt";
            return _effect.PtcN(preview, x, y, z, time);
        }

        internal PTCThread PlayTimeline(
            string key,
            IEfPInteractale listener,
            VariableP variables)
        {
            Stop();
            _timeline = _effect.PtcST(key, listener, PTCThread.StFollow.NO_FOLLOW, variables);
            return _timeline;
        }

        internal EffectItem PlayAttackGhost(string key, float x, float y, float z, int time, AttackGhostDrawer drawer)
        {
            Stop();
            return _effect.setEffectWithSpecificFn(
                "polaris_debug_" + key, x, y, z, time, 0, drawer.FD_EfDraw);
        }

        internal void Stop()
        {
            try
            {
                _timeline?.kill(false);
                _effect?.clear();
            }
            finally
            {
                _timeline = null;
            }
        }

        private void CreatePreviewScene()
        {
            _scene = new GameObject("PEffect Isolated Preview Scene") { layer = PreviewLayer };
            _scene.transform.SetParent(transform, false);
            _scene.transform.position = PreviewOrigin;

            _texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "PolarisParticles .peffect Preview",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _texture.Create();

            var cameraObject = new GameObject("Preview Camera") { layer = PreviewLayer };
            cameraObject.transform.SetParent(_scene.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 32f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.targetTexture = _texture;
            _camera.depth = -100f;

            var effectObject = new GameObject("Preview Effect") { layer = PreviewLayer };
            effectObject.transform.SetParent(_scene.transform, false);
            var meshRenderer = new GameObject("Preview Meshes") { layer = PreviewLayer };
            meshRenderer.transform.SetParent(effectObject.transform, false);

            _effect = new Effect<EffectItem>(effectObject, 256);
            _effect.initEffect("PolarisPEffectPreview", _camera, EffectItem.fnCreateOne, EFCON_TYPE.UI);
            _effect.setLayer(PreviewLayer, PreviewLayer);
            _effect.assignMMRDForMeshDrawerContainer(meshRenderer.AddComponent<MultiMeshRenderer>());
            SetZoom(_zoom);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            try
            {
                Stop();
                _effect?.destruct();
            }
            finally
            {
                _effect = null;
                if (_camera != null)
                    _camera.targetTexture = null;
                if (_texture != null)
                {
                    _texture.Release();
                    Destroy(_texture);
                    _texture = null;
                }
                if (_scene != null)
                    Destroy(_scene);
            }
        }
    }
}
