using System.Collections;
using UnityEngine;

namespace VN
{
    public sealed class VNVfxRuntimeHandle : MonoBehaviour
    {
        public bool IsFinished { get; private set; }

        private ParticleSystem[] _particleSystems;
        private float _resolvedLifetime = -1f;
        private float _softStopSeconds = 0.8f;
        private bool _destroyOnFinish = true;
        private Coroutine _routine;

        public void Initialize(float resolvedLifetime, float softStopSeconds, bool destroyOnFinish = true)
        {
            _resolvedLifetime = resolvedLifetime;
            _softStopSeconds = Mathf.Max(0f, softStopSeconds);
            _destroyOnFinish = destroyOnFinish;
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;

            if (_resolvedLifetime > 0f)
            {
                yield return new WaitForSeconds(_resolvedLifetime);

                SoftStopParticles();

                if (_softStopSeconds > 0f)
                    yield return new WaitForSeconds(_softStopSeconds);

                while (AnyAlive())
                    yield return null;

                Finish();
                yield break;
            }

            if (_particleSystems == null || _particleSystems.Length == 0)
            {
                yield return null;
                Finish();
                yield break;
            }

            while (AnyAlive())
                yield return null;

            Finish();
        }

        private void SoftStopParticles()
        {
            if (_particleSystems == null)
                return;

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var ps = _particleSystems[i];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private bool AnyAlive()
        {
            if (_particleSystems == null)
                return false;

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var ps = _particleSystems[i];
                if (ps != null && ps.IsAlive(true))
                    return true;
            }

            return false;
        }

        private void Finish()
        {
            if (IsFinished)
                return;

            IsFinished = true;

            if (_destroyOnFinish)
                Destroy(gameObject);
        }
    }
}