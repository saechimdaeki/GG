using UnityEngine;
using UnityEngine.Rendering;

namespace LastTrain
{
    public partial class Game
    {
        ParticleSystem _rain;

        void BuildRain()
        {
            var emitter = new GameObject("Street rain");
            emitter.transform.SetParent(transform, false);
            emitter.transform.rotation = Quaternion.Euler(76, 0, -12);
            _rain = emitter.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _rain.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.63f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 17f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.013f, 0.029f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.75f, 0.78f, 0.14f), new Color(0.76f, 0.85f, 0.91f, 0.29f));
            main.maxParticles = 350;
            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(35f, 23f, 0.25f);
            var renderer = _rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.cameraVelocityScale = 0;
            renderer.velocityScale = 0.028f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = Resources.Load<Material>("Materials/FxAlpha");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _rain.Play();
        }

        void MoveRain(float dark)
        {
            bool inMarketOffice = OnMarketUpper || City.MarketGroundRoom.Contains(Me.Pos);
            if (inMarketOffice)
            {
                if (_rain.isPlaying) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            _rain.transform.position = Fx.V3(Me.Pos + Vector2.up * 2f, 10f);
            if (!_rain.isPlaying) _rain.Play();
            var emission = _rain.emission;
            emission.rateOverTime = Mathf.Lerp(160f, 440f, dark);
        }
    }
}
