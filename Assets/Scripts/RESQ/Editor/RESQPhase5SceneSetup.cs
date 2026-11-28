#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RESQ.EditorTools
{
    internal static class RESQPhase5SceneSetup
    {
        private const string TestScenePath = "Assets/Scenes/RESQ_Phase5_Test.unity";
        private const string PrefabFolder = "Assets/_RESQ/Prefabs";
        private const string MaterialsFolder = "Assets/_RESQ/Materials";
        private const string SmallFireSourcePath =
            "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small_Simple.prefab";
        private const string MediumFireSourcePath =
            "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Medium_Simple.prefab";
        private const string SmokeSourcePath =
            "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_Floor_01_Smoke.prefab";
        private const string SmallFirePrefabPath = PrefabFolder + "/RESQ_FireTarget_Small.prefab";
        private const string MediumFirePrefabPath = PrefabFolder + "/RESQ_FireTarget_Medium.prefab";
        private const string SmokePrefabPath = PrefabFolder + "/RESQ_SmokeEmitter.prefab";
        private const string AlarmMaterialPath = MaterialsFolder + "/RESQ_Phase5_Alarm.mat";

        private static readonly HashSet<string> SmokeSystemNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Smoke",
                "Smoke Dark",
                "Smoke Column"
            };

        [MenuItem("RESQ/Phase 5/Inspect Test Scene and VFX _F6")]
        internal static void InspectTestSceneAndVfx()
        {
            if (!TryGetTestScene(out Scene scene))
            {
                return;
            }

            GameObject environment = FindInRoots(scene.GetRootGameObjects(), "Environment");
            GameObject templateEnvironment = environment != null
                ? FindInHierarchy(environment.transform, "Template Environment")
                : null;

            if (environment == null || templateEnvironment == null)
            {
                Debug.LogError("RESQ Phase 5 inspection could not find Environment/Template Environment.", environment);
                return;
            }

            UnityEngine.Object environmentSource =
                PrefabUtility.GetCorrespondingObjectFromSource(templateEnvironment);
            string environmentAssetPath = AssetDatabase.GetAssetPath(environmentSource);
            Renderer[] renderers = templateEnvironment.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = templateEnvironment.GetComponentsInChildren<Collider>(true);

            if (TryGetBounds(renderers, out Bounds bounds))
            {
                Debug.Log(
                    $"RESQ Phase 5 scene validation passed. Active scene: {scene.path}; " +
                    $"environment source: {environmentAssetPath}; world bounds: center={bounds.center}, size={bounds.size}, min={bounds.min}. " +
                    $"Renderers={renderers.Length}, colliders={colliders.Length}.",
                    templateEnvironment);

                foreach (Renderer renderer in renderers)
                {
                    Debug.Log(
                        $"Environment renderer {renderer.name}: center={renderer.bounds.center}, size={renderer.bounds.size}, min={renderer.bounds.min}.",
                        renderer);
                }

                foreach (Collider collider in colliders)
                {
                    Debug.Log(
                        $"Environment collider {collider.name}: center={collider.bounds.center}, size={collider.bounds.size}, trigger={collider.isTrigger}.",
                        collider);
                }
            }
            else
            {
                Debug.LogError(
                    $"RESQ Phase 5 scene has no renderers under Environment. Source: {environmentAssetPath}.",
                    environment);
            }

            InspectParticlePrefab(SmallFireSourcePath);
            InspectParticlePrefab(MediumFireSourcePath);
            InspectParticlePrefab(SmokeSourcePath);
        }

        [MenuItem("RESQ/Phase 5/Build Test Scene Integration _F7")]
        internal static void BuildTestSceneIntegration()
        {
            if (!TryGetTestScene(out Scene scene))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            GameObject environment = FindInRoots(roots, "Environment");
            GameObject templateEnvironment = environment != null
                ? FindInHierarchy(environment.transform, "Template Environment")
                : null;

            if (templateEnvironment == null ||
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(templateEnvironment)) !=
                "Assets/VRTemplateAssets/Models/Environment/Template Environment.fbx")
            {
                Debug.LogError(
                    "Setup stopped: the active test scene does not contain the verified VR Template Environment.",
                    environment);
                return;
            }

            if (FindInRoots(roots, "RESQ_Emergency") != null)
            {
                Debug.LogError(
                    "Setup stopped because RESQ_Emergency already exists in the test scene. No objects were changed.",
                    environment);
                return;
            }

            if (!TryGetBounds(
                    templateEnvironment.GetComponentsInChildren<Renderer>(true),
                    out Bounds environmentBounds))
            {
                Debug.LogError("Setup stopped because Template Environment has no renderable bounds.", templateEnvironment);
                return;
            }

            GameObject smallFireSource = LoadRequiredPrefab(SmallFireSourcePath);
            GameObject mediumFireSource = LoadRequiredPrefab(MediumFireSourcePath);
            GameObject smokeSource = LoadRequiredPrefab(SmokeSourcePath);
            Shader alarmShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (smallFireSource == null || mediumFireSource == null || smokeSource == null || alarmShader == null)
            {
                Debug.LogError("Setup stopped: a required VFX prefab or alarm material shader is missing.");
                return;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialsFolder);

            GameObject smallFirePrefab = CreateFireTargetPrefab(
                smallFireSource,
                SmallFirePrefabPath,
                "RESQ_FireTarget_Small");
            GameObject mediumFirePrefab = CreateFireTargetPrefab(
                mediumFireSource,
                MediumFirePrefabPath,
                "RESQ_FireTarget_Medium");
            GameObject smokePrefab = CreateSmokePrefab(smokeSource);
            Material alarmMaterial = GetOrCreateAlarmMaterial(alarmShader);

            if (smallFirePrefab == null || mediumFirePrefab == null || smokePrefab == null || alarmMaterial == null)
            {
                Debug.LogError("Setup stopped while creating project-owned Phase 5 assets.");
                return;
            }

            GameObject emergencyRoot = new GameObject("RESQ_Emergency");
            SceneManager.MoveGameObjectToScene(emergencyRoot, scene);
            Undo.RegisterCreatedObjectUndo(emergencyRoot, "Create RESQ_Emergency");

            GameObject alarmObject = new GameObject("Temporary Fire Alarm");
            Undo.RegisterCreatedObjectUndo(alarmObject, "Create temporary fire alarm");
            Undo.SetTransformParent(alarmObject.transform, emergencyRoot.transform, "Parent temporary fire alarm");

            GameObject alarmIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            alarmIndicator.name = "Alarm Indicator Visual";
            Undo.RegisterCreatedObjectUndo(alarmIndicator, "Create alarm indicator visual");
            Undo.SetTransformParent(alarmIndicator.transform, alarmObject.transform, "Parent alarm indicator visual");
            alarmIndicator.transform.localPosition = Vector3.zero;
            alarmIndicator.transform.localRotation = Quaternion.identity;
            alarmIndicator.transform.localScale = Vector3.one * 0.18f;

            Collider indicatorCollider = alarmIndicator.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                Undo.DestroyObjectImmediate(indicatorCollider);
            }

            Renderer alarmRenderer = alarmIndicator.GetComponent<Renderer>();
            alarmRenderer.sharedMaterial = alarmMaterial;

            Light alarmLight = Undo.AddComponent<Light>(alarmObject);
            alarmLight.type = LightType.Point;
            alarmLight.color = new Color(1f, 0.08f, 0.04f, 1f);
            alarmLight.intensity = 1.5f;
            alarmLight.range = 3f;
            alarmLight.shadows = LightShadows.None;
            alarmLight.enabled = false;

            GameObject fireControllerObject = new GameObject("Fire Controller");
            Undo.RegisterCreatedObjectUndo(fireControllerObject, "Create fire controller");
            Undo.SetTransformParent(fireControllerObject.transform, emergencyRoot.transform, "Parent fire controller");
            FireController fireController = Undo.AddComponent<FireController>(fireControllerObject);

            EmergencyStateManager emergencyStateManager =
                Undo.AddComponent<EmergencyStateManager>(emergencyRoot);
            FireAlarmController alarmController = Undo.AddComponent<FireAlarmController>(alarmObject);
            ConfigureAlarmController(alarmController, alarmLight, alarmRenderer);

            Vector3 firePositionA = GetFloorPoint(templateEnvironment.transform, environmentBounds, 0.38f, 0.46f);
            Vector3 firePositionB = GetFloorPoint(templateEnvironment.transform, environmentBounds, 0.62f, 0.54f);
            FireSuppressionTarget fireA = InstantiateFireTarget(
                smallFirePrefab,
                emergencyRoot.transform,
                scene,
                "Fire Target A",
                firePositionA);
            FireSuppressionTarget fireB = InstantiateFireTarget(
                mediumFirePrefab,
                emergencyRoot.transform,
                scene,
                "Fire Target B",
                firePositionB);

            ConfigureFireController(fireController, fireA, fireB);
            ConfigureEmergencyStateManager(emergencyStateManager, alarmController, fireController);

            Vector3 smokePosition = Vector3.Lerp(firePositionA, firePositionB, 0.5f);
            GameObject smokeInstance = PrefabUtility.InstantiatePrefab(smokePrefab, scene) as GameObject;
            if (smokeInstance == null)
            {
                Undo.DestroyObjectImmediate(emergencyRoot);
                Debug.LogError("Setup stopped because the smoke prefab could not be instantiated.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(smokeInstance, "Create smoke emitter");
            Undo.SetTransformParent(smokeInstance.transform, emergencyRoot.transform, "Parent smoke emitter");
            Undo.RecordObject(smokeInstance.transform, "Place smoke emitter");
            smokeInstance.transform.position = smokePosition;

            SmokeController smokeController = smokeInstance.GetComponent<SmokeController>();
            if (smokeController == null)
            {
                Undo.DestroyObjectImmediate(emergencyRoot);
                Undo.DestroyObjectImmediate(smokeInstance);
                Debug.LogError("Setup stopped because the smoke prefab has no SmokeController.");
                return;
            }

            ConfigureSmokeController(smokeController, emergencyStateManager);

            Vector3 alarmPosition = environmentBounds.center +
                                    Vector3.up * Mathf.Max(0.5f, environmentBounds.extents.y * 0.35f);
            Undo.RecordObject(alarmObject.transform, "Place temporary fire alarm");
            alarmObject.transform.position = alarmPosition;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"Failed to save {TestScenePath}. The original RESQ_Main scene was not opened.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Phase 5 integration created in {TestScenePath}. " +
                $"Fire targets: {firePositionA} and {firePositionB}; smoke: {smokePosition}; alarm visual: {alarmPosition}. " +
                "Use the EmergencyStateManager component context menu in Play Mode to start/reset.",
                emergencyRoot);
        }

        [MenuItem("RESQ/Phase 5/Test Phase 5 in Play Mode _F8")]
        private static void TestPhase5InPlayMode()
        {
            if (!TryGetTestScene(out Scene scene))
            {
                return;
            }

            if (FindInRoots(scene.GetRootGameObjects(), "RESQ_Emergency") == null)
            {
                Debug.LogError("Phase 5 Play Mode test stopped: RESQ_Emergency is missing.");
                return;
            }

            testPhaseFrame = 0;
            testPhasePassCount = 0;
            testPhaseFailCount = 0;
            testManager = null;
            testFireController = null;
            testSmokeController = null;
            testAlarmController = null;

            EditorApplication.playModeStateChanged -= OnPhase5PlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPhase5PlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        private static int testPhaseFrame;
        private static int testPhasePassCount;
        private static int testPhaseFailCount;
        private static EmergencyStateManager testManager;
        private static FireController testFireController;
        private static SmokeController testSmokeController;
        private static FireAlarmController testAlarmController;

        private static void OnPhase5PlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.update -= OnPhase5TestUpdate;
                EditorApplication.update += OnPhase5TestUpdate;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= OnPhase5TestUpdate;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPhase5PlayModeStateChanged;
                Debug.Log(
                    $"Phase 5 Play Mode test finished: {testPhasePassCount} passed, {testPhaseFailCount} failed.");
            }
        }

        private static void OnPhase5TestUpdate()
        {
            testPhaseFrame++;

            if (testPhaseFrame == 1)
            {
                testManager = UnityEngine.Object.FindAnyObjectByType<EmergencyStateManager>();
                testFireController = UnityEngine.Object.FindAnyObjectByType<FireController>();
                testSmokeController = UnityEngine.Object.FindAnyObjectByType<SmokeController>();
                testAlarmController = UnityEngine.Object.FindAnyObjectByType<FireAlarmController>();

                CheckPhase5(
                    testManager != null &&
                    testFireController != null &&
                    testSmokeController != null &&
                    testAlarmController != null,
                    "All four Phase 5 controllers are present in Play Mode.");
            }
            else if (testPhaseFrame == 5)
            {
                if (testManager == null)
                {
                    FailAndExit("EmergencyStateManager reference was lost.");
                    return;
                }

                testManager.ResetEmergency();
                CheckPhase5(testManager.CurrentState == EmergencyState.Inactive, "Emergency starts Inactive.");
                CheckPhase5(!testFireController.FireIsActive, "Fire starts inactive.");
                CheckPhase5(!testSmokeController.SmokeIsActive, "Smoke starts inactive.");
                CheckPhase5(!testAlarmController.AlarmIsActive, "Alarm starts inactive.");
            }
            else if (testPhaseFrame == 10)
            {
                testManager.StartEmergency();
                CheckPhase5(testManager.CurrentState == EmergencyState.AlarmActive, "Start Emergency enters AlarmActive.");
                CheckPhase5(testAlarmController.AlarmIsActive, "Emergency start activates alarm.");
                CheckPhase5(testFireController.FireIsActive, "Emergency start activates fire.");
                CheckPhase5(testSmokeController.SmokeIsActive, "Emergency start activates smoke.");
            }
            else if (testPhaseFrame == 15)
            {
                testManager.BeginEvacuation();
                CheckPhase5(
                    testManager.CurrentState == EmergencyState.EvacuationActive,
                    "Begin Evacuation enters EvacuationActive.");
            }
            else if (testPhaseFrame == 20)
            {
                testManager.CompleteEvacuation();
                CheckPhase5(
                    testManager.CurrentState == EmergencyState.Completed,
                    "Complete Evacuation enters Completed.");
            }
            else if (testPhaseFrame == 25)
            {
                foreach (FireSuppressionTarget target in testFireController.Targets)
                {
                    target.ApplySuppression(target.MaxSuppression);
                }

                CheckPhase5(
                    testFireController.GetOverallSuppressionProgress() >= 1f,
                    "Applying suppression reaches 100 percent.");
            }
            else if (testPhaseFrame == 30)
            {
                testManager.ResetEmergency();
                CheckPhase5(testManager.CurrentState == EmergencyState.Inactive, "Reset returns to Inactive.");
                CheckPhase5(!testAlarmController.AlarmIsActive, "Reset stops alarm.");
                CheckPhase5(!testFireController.FireIsActive, "Reset stops fire.");
                CheckPhase5(!testSmokeController.SmokeIsActive, "Reset stops and clears smoke.");
            }
            else if (testPhaseFrame == 35)
            {
                testManager.StartEmergency();
                CheckPhase5(testManager.CurrentState == EmergencyState.AlarmActive, "Second emergency starts successfully.");
                CheckPhase5(testAlarmController.AlarmIsActive, "Second emergency restarts alarm.");
                CheckPhase5(testFireController.FireIsActive, "Second emergency restarts fire.");
                CheckPhase5(testSmokeController.SmokeIsActive, "Second emergency restarts smoke.");
                testManager.ResetEmergency();
            }
            else if (testPhaseFrame == 40)
            {
                CheckPhase5(testManager.CurrentState == EmergencyState.Inactive, "Test leaves emergency Inactive.");
                EditorApplication.ExitPlaymode();
            }
        }

        private static void CheckPhase5(bool passed, string message)
        {
            if (passed)
            {
                testPhasePassCount++;
                Debug.Log($"Phase 5 Play Mode PASS: {message}");
            }
            else
            {
                testPhaseFailCount++;
                Debug.LogError($"Phase 5 Play Mode FAIL: {message}");
            }
        }

        private static void FailAndExit(string message)
        {
            testPhaseFailCount++;
            Debug.LogError($"Phase 5 Play Mode FAIL: {message}");
            EditorApplication.ExitPlaymode();
        }

        private static bool TryGetTestScene(out Scene scene)
        {
            scene = EditorSceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Phase 5 setup requires Edit Mode.");
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded || scene.path != TestScenePath)
            {
                Debug.LogError(
                    $"Phase 5 setup is restricted to the loaded {TestScenePath}. Active scene: {scene.path}");
                return false;
            }

            return true;
        }

        private static GameObject FindInRoots(GameObject[] roots, string objectName)
        {
            foreach (GameObject root in roots)
            {
                GameObject found = FindInHierarchy(root.transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindInHierarchy(Transform root, string objectName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool foundRenderer = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!foundRenderer)
                {
                    bounds = renderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return foundRenderer;
        }

        private static Vector3 GetFloorPoint(
            Transform environmentRoot,
            Bounds bounds,
            float xFraction,
            float zFraction)
        {
            Vector3 candidate = new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, xFraction),
                bounds.min.y + 0.03f,
                Mathf.Lerp(bounds.min.z, bounds.max.z, zFraction));

            Vector3 rayOrigin = candidate;
            rayOrigin.y = bounds.max.y + Mathf.Max(2f, bounds.size.y);
            float rayDistance = bounds.size.y + Mathf.Max(10f, bounds.size.y);
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    !hit.collider.transform.IsChildOf(environmentRoot) ||
                    hit.normal.y < 0.65f ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                candidate = hit.point + Vector3.up * 0.03f;
                nearestDistance = hit.distance;
            }

            return candidate;
        }

        private static void InspectParticlePrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"VFX prefab not found: {path}");
                return;
            }

            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            List<string> descriptions = new List<string>(systems.Length);

            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                descriptions.Add($"{system.gameObject.name}(playOnAwake={main.playOnAwake})");
            }

            Debug.Log(
                $"VFX hierarchy {path}: {systems.Length} ParticleSystems: {string.Join(", ", descriptions)}",
                prefab);
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Required VFX prefab is missing: {path}");
            }

            return prefab;
        }

        private static GameObject CreateFireTargetPrefab(
            GameObject source,
            string destinationPath,
            string prefabName)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
            if (existing != null)
            {
                return existing.GetComponent<FireSuppressionTarget>() != null
                    ? existing
                    : LogInvalidExistingPrefab(destinationPath);
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject wrapper = new GameObject(prefabName);
                SceneManager.MoveGameObjectToScene(wrapper, previewScene);

                GameObject visualRoot = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
                if (visualRoot == null)
                {
                    Debug.LogError($"Could not instantiate source fire prefab {source.name}.");
                    return null;
                }

                visualRoot.transform.SetParent(wrapper.transform, false);
                visualRoot.name = source.name;
                ParticleSystem[] systems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
                PrepareParticleSystems(systems);
                visualRoot.SetActive(false);

                FireSuppressionTarget target = wrapper.AddComponent<FireSuppressionTarget>();
                SerializedObject serializedTarget = new SerializedObject(target);
                SetObjectArray(serializedTarget, "fireVisualRoots", new UnityEngine.Object[] { visualRoot });
                SetObjectArray(serializedTarget, "fireParticleSystems", systems);
                serializedTarget.FindProperty("activateVisualsOnEnable").boolValue = false;
                serializedTarget.ApplyModifiedProperties();

                return SavePrefabAsset(wrapper, destinationPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static GameObject CreateSmokePrefab(GameObject source)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<SmokeController>() != null
                    ? existing
                    : LogInvalidExistingPrefab(SmokePrefabPath);
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject wrapper = new GameObject("RESQ_SmokeEmitter");
                SceneManager.MoveGameObjectToScene(wrapper, previewScene);

                GameObject visualRoot = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
                if (visualRoot == null)
                {
                    Debug.LogError($"Could not instantiate source smoke prefab {source.name}.");
                    return null;
                }

                visualRoot.transform.SetParent(wrapper.transform, false);
                visualRoot.name = source.name;
                ParticleSystem[] allSystems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
                PrepareParticleSystems(allSystems);

                List<ParticleSystem> smokeSystems = new List<ParticleSystem>();
                foreach (ParticleSystem system in allSystems)
                {
                    if (SmokeSystemNames.Contains(system.gameObject.name))
                    {
                        smokeSystems.Add(system);
                    }
                }

                if (smokeSystems.Count == 0)
                {
                    Debug.LogError(
                        $"No configured smoke ParticleSystems were found in {source.name}. Expected: {string.Join(", ", SmokeSystemNames)}.");
                    return null;
                }

                SmokeController controller = wrapper.AddComponent<SmokeController>();
                SerializedObject serializedController = new SerializedObject(controller);
                SetObjectArray(serializedController, "smokeParticleSystems", smokeSystems.ToArray());
                serializedController.ApplyModifiedProperties();

                return SavePrefabAsset(wrapper, SmokePrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void PrepareParticleSystems(ParticleSystem[] systems)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                PrefabUtility.RecordPrefabInstancePropertyModifications(system);
            }
        }

        private static GameObject SavePrefabAsset(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError($"Could not save project-owned prefab: {path}");
                return null;
            }

            return prefab;
        }

        private static GameObject LogInvalidExistingPrefab(string path)
        {
            Debug.LogError(
                $"A project-owned asset already exists at {path}, but it does not have the expected controller. It was not overwritten.");
            return null;
        }

        private static Material GetOrCreateAlarmMaterial(Shader shader)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(AlarmMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Material material = new Material(shader)
            {
                name = "RESQ_Phase5_Alarm"
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.35f, 0.02f, 0.01f, 1f));
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.35f, 0.02f, 0.01f, 1f));
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            material.DisableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, AlarmMaterialPath);
            return material;
        }

        private static void ConfigureAlarmController(
            FireAlarmController controller,
            Light alarmLight,
            Renderer alarmRenderer)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            SetObjectArray(serializedController, "alarmLights", new UnityEngine.Object[] { alarmLight });
            SetObjectArray(serializedController, "emissiveRenderers", new UnityEngine.Object[] { alarmRenderer });
            serializedController.ApplyModifiedProperties();
        }

        private static void ConfigureFireController(
            FireController controller,
            FireSuppressionTarget fireA,
            FireSuppressionTarget fireB)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            SetObjectArray(serializedController, "targets", new UnityEngine.Object[] { fireA, fireB });
            serializedController.ApplyModifiedProperties();
        }

        private static void ConfigureEmergencyStateManager(
            EmergencyStateManager manager,
            FireAlarmController alarmController,
            FireController fireController)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("alarmController").objectReferenceValue = alarmController;
            serializedManager.FindProperty("fireController").objectReferenceValue = fireController;
            serializedManager.FindProperty("emergencyTimeoutSeconds").floatValue = 0f;
            serializedManager.ApplyModifiedProperties();
        }

        private static void ConfigureSmokeController(
            SmokeController controller,
            EmergencyStateManager manager)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("emergencyStateManager").objectReferenceValue = manager;
            serializedController.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        private static FireSuppressionTarget InstantiateFireTarget(
            GameObject prefab,
            Transform parent,
            Scene scene,
            string instanceName,
            Vector3 position)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate {prefab.name} in {scene.path}.");
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Create {instanceName}");
            Undo.SetTransformParent(instance.transform, parent, $"Parent {instanceName}");
            Undo.RecordObject(instance.transform, $"Place {instanceName}");
            instance.name = instanceName;
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            FireSuppressionTarget target = instance.GetComponent<FireSuppressionTarget>();
            if (target == null)
            {
                throw new InvalidOperationException($"{prefab.name} does not contain FireSuppressionTarget.");
            }

            return target;
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Length;

            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            {
                throw new InvalidOperationException($"Parent folder does not exist for {path}.");
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

}
#endif
