using UnityEngine;
using XX;

namespace Polaris.Particles.Effects
{
    /// <summary>
    /// 一个只报告固定坐标的 <see cref="IEfPInteractale"/>，让固定坐标播放也能响应
    /// SETTER 的位置指令与重定位请求。要跟着会动的东西跑用
    /// <see cref="EffectDrawTargetAnchor"/>。
    /// </summary>
    internal sealed class EffectPositionAnchor : IEfPInteractale
    {
        private readonly Vector3 _position;

        internal EffectPositionAnchor(float x, float y)
        {
            _position = new Vector3(x, y, 0f);
        }

        public string snd_key => string.Empty;

        public bool getEffectReposition(PTCThread St, PTCThread.StFollow follow, float fcnt, out Vector3 V)
        {
            V = _position;
            return true;
        }

        public bool readPtcScript(PTCThread rER)
        {
            if (rER.cmd == "%MYPOS")
            {
                rER.Def("cx", _position.x);
                rER.Def("cy", _position.y);
                return true;
            }
            if (rER.cmd == "%CALCPOS")
            {
                rER.Def("x", _position.x);
                rER.Def("y", _position.y);
                return true;
            }
            return false;
        }

        public bool isSoundActive(SndPlayer S) => false;

        public bool initSetEffect(PTCThread Thread, EffectItem Ef) => true;
    }
}
