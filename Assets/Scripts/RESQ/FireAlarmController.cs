using UnityEngine;
using UnityEngine.Events;

namespace RESQ
{
    public sealed class FireAlarmController : MonoBehaviour
    {
        [SerializeField] private AudioSource alarmAudioSource;
        [SerializeField] private AudioClip alarmClip;
        [SerializeField] private Light[] alarmLights;
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private Color alarmEmissionColor = new Color(1f, 0.1f, 0.1f, 1f);
        [SerializeField, Min(0f)] private float alarmEmissionIntensity = 2f;

        [SerializeField] private UnityEvent alarmActivated;
        [SerializeField] private UnityEvent alarmDeactivated;

        private bool alarmIsActive;
        private Color[] originalEmissionColors;
        private bool[] emissiveKeywordsEnabled;
        private bool[] originalLightEnabledStates;
        private AudioClip originalAudioClip;
        private bool originalAudioLoop;
        private Material[] originalSharedMaterials;

        public bool AlarmIsActive => alarmIsActive;
        public UnityEvent AlarmActivated => alarmActivated;
        public UnityEvent AlarmDeactivated => alarmDeactivated;

        private void Awake()
        {
            CacheEmissiveState();
            CacheLightAndAudioState();
        }

        [ContextMenu("Activate Alarm")]
        public void ActivateAlarm()
        {
            if (alarmIsActive)
            {
                return;
            }

            alarmIsActive = true;

            if (alarmLights != null)
            {
                foreach (Light alarmLight in alarmLights)
                {
                    if (alarmLight != null)
                    {
                        alarmLight.enabled = true;
                    }
                }
            }

            if (alarmAudioSource != null)
            {
                alarmAudioSource.loop = true;
                alarmAudioSource.clip = alarmClip;

                if (!alarmAudioSource.isPlaying)
                {
                    alarmAudioSource.Play();
                }
            }

            SetEmissiveColor(true);
            alarmActivated?.Invoke();
        }

        [ContextMenu("Deactivate Alarm")]
        public void DeactivateAlarm()
        {
            if (!alarmIsActive)
            {
                return;
            }

            alarmIsActive = false;

            if (alarmLights != null)
            {
                foreach (Light alarmLight in alarmLights)
                {
                    if (alarmLight != null)
                    {
                        alarmLight.enabled = false;
                    }
                }
            }

            if (alarmAudioSource != null)
            {
                alarmAudioSource.Stop();
            }

            SetEmissiveColor(false);
            alarmDeactivated?.Invoke();
        }

        [ContextMenu("Reset Alarm")]
        public void ResetAlarm()
        {
            alarmIsActive = false;

            if (alarmLights != null)
            {
                for (int index = 0; index < alarmLights.Length; index++)
                {
                    Light alarmLight = alarmLights[index];
                    if (alarmLight != null)
                    {
                        if (originalLightEnabledStates != null &&
                            index < originalLightEnabledStates.Length)
                        {
                            alarmLight.enabled = originalLightEnabledStates[index];
                        }
                        else
                        {
                            alarmLight.enabled = false;
                        }
                    }
                }
            }

            if (alarmAudioSource != null)
            {
                alarmAudioSource.Stop();
                alarmAudioSource.clip = originalAudioClip;
                alarmAudioSource.loop = originalAudioLoop;
            }

            RestoreEmissiveState();
        }

        private void CacheLightAndAudioState()
        {
            if (alarmLights != null)
            {
                originalLightEnabledStates = new bool[alarmLights.Length];

                for (int index = 0; index < alarmLights.Length; index++)
                {
                    Light alarmLight = alarmLights[index];
                    if (alarmLight != null)
                    {
                        originalLightEnabledStates[index] = alarmLight.enabled;
                    }
                }
            }

            if (alarmAudioSource != null)
            {
                originalAudioClip = alarmAudioSource.clip;
                originalAudioLoop = alarmAudioSource.loop;
            }
        }

        private void CacheEmissiveState()
        {
            if (emissiveRenderers == null)
            {
                return;
            }

            originalEmissionColors = new Color[emissiveRenderers.Length];
            emissiveKeywordsEnabled = new bool[emissiveRenderers.Length];
            originalSharedMaterials = new Material[emissiveRenderers.Length];

            for (int index = 0; index < emissiveRenderers.Length; index++)
            {
                Renderer renderer = emissiveRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Material material = renderer.sharedMaterial;
                if (material == null)
                {
                    continue;
                }

                originalEmissionColors[index] = material.HasProperty("_EmissionColor")
                    ? material.GetColor("_EmissionColor")
                    : Color.black;
                emissiveKeywordsEnabled[index] = material.IsKeywordEnabled("_EMISSION");
                originalSharedMaterials[index] = renderer.sharedMaterial;
            }
        }

        private void SetEmissiveColor(bool active)
        {
            if (emissiveRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in emissiveRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Material material = renderer.material;
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor(
                        "_EmissionColor",
                        active
                            ? alarmEmissionColor * alarmEmissionIntensity
                            : Color.black);
                }

                if (active)
                {
                    material.EnableKeyword("_EMISSION");
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                }
            }
        }

        private void RestoreEmissiveState()
        {
            if (emissiveRenderers == null)
            {
                return;
            }

            for (int index = 0; index < emissiveRenderers.Length; index++)
            {
                Renderer renderer = emissiveRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (originalSharedMaterials != null &&
                    index < originalSharedMaterials.Length &&
                    originalSharedMaterials[index] != null)
                {
                    renderer.sharedMaterial = originalSharedMaterials[index];
                }

                if (renderer.material == null)
                {
                    continue;
                }

                if (renderer.material.HasProperty("_EmissionColor") &&
                    originalEmissionColors != null &&
                    index < originalEmissionColors.Length)
                {
                    renderer.material.SetColor(
                        "_EmissionColor",
                        originalEmissionColors[index]);
                }

                if (emissiveKeywordsEnabled != null &&
                    index < emissiveKeywordsEnabled.Length)
                {
                    if (emissiveKeywordsEnabled[index])
                    {
                        renderer.material.EnableKeyword("_EMISSION");
                    }
                    else
                    {
                        renderer.material.DisableKeyword("_EMISSION");
                    }
                }
            }
        }
    }
}
