using UnityEngine;

namespace TowerDefense.UI
{
    public class FlickeringTorch : MonoBehaviour
    {
        [Header("Flame Settings")]
        [SerializeField] private float _minLightIntensity = 1.2f;
        [SerializeField] private float _maxLightIntensity = 2.0f;
        [SerializeField] private float _flickerSpeed = 5f;
        [SerializeField] private Color _flameColor = new Color(1f, 0.6f, 0.1f, 1f);
        [SerializeField] private Color _flameTipColor = new Color(1f, 0.9f, 0.3f, 1f);

        private Light _pointLight;
        private ParticleSystem _flameParticles;
        private float _randomOffset;

        private void Start()
        {
            _randomOffset = Random.Range(0f, 100f);
            SetupPole();
            SetupBowl();
            SetupFlame();
            SetupLight();
        }

        private void SetupPole()
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(transform, false);
            pole.transform.localPosition = new Vector3(0, -0.5f, 0);
            pole.transform.localScale = new Vector3(0.08f, 0.5f, 0.08f);

            Destroy(pole.GetComponent<Collider>());
            Renderer rend = pole.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Standard"));
            rend.material.color = new Color(0.5f, 0.45f, 0.4f);
            rend.material.SetFloat("_Glossiness", 0.1f);
        }

        private void SetupBowl()
        {
            GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "Bowl";
            bowl.transform.SetParent(transform, false);
            bowl.transform.localPosition = new Vector3(0, 0.05f, 0);
            bowl.transform.localScale = new Vector3(0.15f, 0.04f, 0.15f);

            Destroy(bowl.GetComponent<Collider>());
            Renderer rend = bowl.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Standard"));
            rend.material.color = new Color(0.4f, 0.35f, 0.3f);
            rend.material.SetFloat("_Glossiness", 0.2f);
        }

        private void SetupFlame()
        {
            GameObject flameObj = new GameObject("Flame");
            flameObj.transform.SetParent(transform, false);
            flameObj.transform.localPosition = new Vector3(0, 0.15f, 0);

            _flameParticles = flameObj.AddComponent<ParticleSystem>();

            // Stop before modifying duration to avoid assert
            _flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _flameParticles.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(_flameColor, _flameTipColor);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.3f, -0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 50;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var shape = _flameParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.03f;
            shape.length = 0.1f;

            var colorOverLifetime = _flameParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(_flameColor, 0f),
                    new GradientColorKey(_flameTipColor, 0.5f),
                    new GradientColorKey(new Color(1f, 0.3f, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = _flameParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(0.3f, 0.9f);
            sizeCurve.AddKey(1f, 0.1f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var velocityOverLifetime = _flameParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var emission = _flameParticles.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(25f, 40f);

            var renderer = _flameParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.SetFloat("_Mode", 1);
            renderer.material.color = _flameColor;
            renderer.trailMaterial = null;

            _flameParticles.Play();
        }

        private void SetupLight()
        {
            GameObject lightObj = new GameObject("TorchLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0, 0.25f, 0);

            _pointLight = lightObj.AddComponent<Light>();
            _pointLight.type = LightType.Point;
            _pointLight.intensity = 1.5f;
            _pointLight.range = 6f;
            _pointLight.color = new Color(1f, 0.7f, 0.3f);
            _pointLight.shadows = LightShadows.Soft;
            _pointLight.shadowStrength = 0.5f;
        }

        private void Update()
        {
            if (_pointLight == null) return;

            float noise = Mathf.PerlinNoise(Time.time * _flickerSpeed, _randomOffset);
            _pointLight.intensity = Mathf.Lerp(_minLightIntensity, _maxLightIntensity, noise);

            float sizeNoise = Mathf.PerlinNoise(Time.time * 3f, _randomOffset + 50f);
            if (_flameParticles != null)
            {
                var main = _flameParticles.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f + sizeNoise * 0.02f, 0.12f + sizeNoise * 0.06f);
            }
        }
    }
}