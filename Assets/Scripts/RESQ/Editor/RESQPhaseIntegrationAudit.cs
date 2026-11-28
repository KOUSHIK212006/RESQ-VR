#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using RESQ;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RESQ.EditorTools
{
    public static class RESQPhaseIntegrationAudit
    {
        private const string TestScenePath = "Assets/Scenes/RESQ_Phase5_Test.unity";

        private static readonly string[] ControlNames =
        {
            "Start Emergency",
            "Begin Evacuation",
            "Reset Emergency"
        };

        private static readonly EmergencyStateControl.Action[] ControlActions =
        {
            EmergencyStateControl.Action.StartEmergency,
            EmergencyStateControl.Action.BeginEvacuation,
            EmergencyStateControl.Action.ResetEmergency
        };

        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> warnings = new List<string>();
        private static int passes;

        [MenuItem("RESQ/Validate Phase 5 & 6 Integration")]
        public static void ValidateFromMenu()
        {
            RunAudit();
        }

        public static void ValidateBatchmode()
        {
            bool passed = RunAudit();
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static bool RunAudit()
        {
            failures.Clear();
            warnings.Clear();
            passes = 0;

            Scene scene = SceneManager.GetSceneByPath(TestScenePath);
            bool openedForAudit = !scene.IsValid() || !scene.isLoaded;

            try
            {
                if (openedForAudit)
                {
                    scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Fail($"Could not load {TestScenePath}.");
                    return LogSummary();
                }

                AuditScene(scene);
                return LogSummary();
            }
            catch (Exception exception)
            {
                Fail($"Audit could not complete: {exception.Message}");
                Debug.LogException(exception);
                return LogSummary();
            }
            finally
            {
                if (openedForAudit && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AuditScene(Scene scene)
        {
            List<GameObject> gameObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToList();

            foreach (GameObject gameObject in gameObjects)
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount > 0)
                {
                    Fail($"{gameObject.name} has {missingCount} missing script component(s).");
                }
            }

            if (!failures.Any(failure => failure.Contains("missing script component")))
            {
                Pass("No missing script components.");
            }

            EmergencyStateManager[] stateManagers = GetComponents<EmergencyStateManager>(gameObjects);
            FireController[] fireControllers = GetComponents<FireController>(gameObjects);
            FireAlarmController[] alarmControllers = GetComponents<FireAlarmController>(gameObjects);
            SmokeController[] smokeControllers = GetComponents<SmokeController>(gameObjects);
            FireSuppressionTarget[] fireTargets = GetComponents<FireSuppressionTarget>(gameObjects);
            EvacuationManager[] evacuationManagers = GetComponents<EvacuationManager>(gameObjects);
            EvacuationWaypoint[] waypoints = GetComponents<EvacuationWaypoint>(gameObjects);
            AssemblyPointTrigger[] assemblyTriggers = GetComponents<AssemblyPointTrigger>(gameObjects);
            EvacuationStatusUI[] statusUis = GetComponents<EvacuationStatusUI>(gameObjects);
            EmergencyDoorController[] doorControllers = GetComponents<EmergencyDoorController>(gameObjects);
            FireExtinguisherController[] extinguishers = GetComponents<FireExtinguisherController>(gameObjects);
            XRInteractionManager[] interactionManagers = GetComponents<XRInteractionManager>(gameObjects);

            Check(stateManagers.Length == 1, $"Exactly one EmergencyStateManager exists (found {stateManagers.Length}).");
            Check(interactionManagers.Length == 1, $"Exactly one XRInteractionManager exists (found {interactionManagers.Length}).");

            EmergencyStateManager stateManager = stateManagers.FirstOrDefault();
            FireController fireController = fireControllers.FirstOrDefault();
            FireAlarmController alarmController = alarmControllers.FirstOrDefault();
            SmokeController smokeController = smokeControllers.FirstOrDefault();
            EvacuationManager evacuationManager = evacuationManagers.FirstOrDefault();
            XRInteractionManager interactionManager = interactionManagers.FirstOrDefault();

            Check(fireControllers.Length == 1, $"Exactly one FireController exists (found {fireControllers.Length}).");
            Check(alarmControllers.Length == 1, $"Exactly one FireAlarmController exists (found {alarmControllers.Length}).");
            Check(smokeControllers.Length == 1, $"Exactly one SmokeController exists (found {smokeControllers.Length}).");
            Check(evacuationManagers.Length == 1, $"Exactly one EvacuationManager exists (found {evacuationManagers.Length}).");

            if (stateManager != null)
            {
                Check(ReadReference(stateManager, "alarmController") == alarmController,
                    "EmergencyStateManager references the scene FireAlarmController.");
                Check(ReadReference(stateManager, "fireController") == fireController,
                    "EmergencyStateManager references the scene FireController.");
                Check(stateManager.CurrentState == EmergencyState.Inactive,
                    "EmergencyStateManager starts Inactive.");
            }

            AuditControls(gameObjects, stateManager, interactionManager);
            AuditFireAndAlarm(fireController, alarmController, fireTargets);
            AuditSmoke(smokeController, stateManager);
            AuditEvacuation(
                evacuationManager,
                waypoints,
                assemblyTriggers,
                stateManager,
                gameObjects);
            AuditDoor(doorControllers, stateManager, interactionManager);
            AuditExtinguisher(extinguishers, stateManager, fireController, fireTargets);
            AuditStatusUi(statusUis, stateManager, evacuationManager);
        }

        private static void AuditControls(
            List<GameObject> gameObjects,
            EmergencyStateManager stateManager,
            XRInteractionManager interactionManager)
        {
            for (int index = 0; index < ControlNames.Length; index++)
            {
                GameObject[] matches = gameObjects
                    .Where(gameObject => gameObject.name == ControlNames[index])
                    .ToArray();
                Check(matches.Length == 1,
                    $"{ControlNames[index]} exists exactly once (found {matches.Length}).");
                if (matches.Length != 1)
                {
                    continue;
                }

                GameObject controlObject = matches[0];
                BoxCollider boxCollider = controlObject.GetComponent<BoxCollider>();
                XRSimpleInteractable interactable = controlObject.GetComponent<XRSimpleInteractable>();
                EmergencyStateControl control = controlObject.GetComponent<EmergencyStateControl>();

                Check(boxCollider != null, $"{ControlNames[index]} has a BoxCollider.");
                Check(interactable != null, $"{ControlNames[index]} has an XR Simple Interactable.");
                Check(control != null, $"{ControlNames[index]} has an EmergencyStateControl.");

                if (control != null)
                {
                    Check(ReadReference(control, "emergencyStateManager") == stateManager,
                        $"{ControlNames[index]} references the shared EmergencyStateManager.");
                    Check(ReadReference(control, "interactable") == interactable,
                        $"{ControlNames[index]} references its own interactable.");
                    SerializedProperty action = new SerializedObject(control).FindProperty("action");
                    Check(action != null &&
                          action.enumValueIndex == (int)ControlActions[index],
                        $"{ControlNames[index]} uses action {ControlActions[index]}.");
                }

                if (interactable != null)
                {
                    Check(ReadReference(interactable, "m_InteractionManager") == interactionManager,
                        $"{ControlNames[index]} uses the scene XRInteractionManager.");
                    Check(HasArrayReference(interactable, "m_Colliders", boxCollider),
                        $"{ControlNames[index]} registers its BoxCollider with the interactable.");
                }
            }
        }

        private static void AuditFireAndAlarm(
            FireController fireController,
            FireAlarmController alarmController,
            FireSuppressionTarget[] fireTargets)
        {
            if (fireController == null)
            {
                return;
            }

            SerializedObject serializedFire = new SerializedObject(fireController);
            SerializedProperty configuredTargets = serializedFire.FindProperty("targets");
            Check(configuredTargets != null && configuredTargets.arraySize > 0,
                "FireController has configured suppression targets.");

            if (configuredTargets != null)
            {
                for (int index = 0; index < configuredTargets.arraySize; index++)
                {
                    FireSuppressionTarget target =
                        configuredTargets.GetArrayElementAtIndex(index).objectReferenceValue as FireSuppressionTarget;
                    Check(target != null, $"FireController target {index} is assigned.");
                    if (target != null)
                    {
                        Check(fireTargets.Contains(target), $"FireController target {target.name} exists in the scene.");
                    }
                }
            }

            Check(!fireController.FireIsActive, "Fire starts inactive.");
            foreach (FireSuppressionTarget target in fireTargets)
            {
                Check(!target.IsSuppressed, $"Fire target {target.name} starts unsuppressed.");
            }

            if (alarmController == null)
            {
                return;
            }

            SerializedObject serializedAlarm = new SerializedObject(alarmController);
            Check(!alarmController.AlarmIsActive, "Alarm starts inactive.");
            Check(HasAssignedArray(serializedAlarm, "alarmLights"),
                "FireAlarmController has alarm lights assigned.");
            Check(HasAssignedArray(serializedAlarm, "emissiveRenderers"),
                "FireAlarmController has emissive renderers assigned.");

            SerializedProperty lights = serializedAlarm.FindProperty("alarmLights");
            if (lights != null)
            {
                for (int index = 0; index < lights.arraySize; index++)
                {
                    Light light = lights.GetArrayElementAtIndex(index).objectReferenceValue as Light;
                    Check(light != null, $"Alarm light {index} is assigned.");
                    if (light != null)
                    {
                        Check(!light.enabled, $"Alarm light {light.name} starts disabled.");
                    }
                }
            }

            if (ReadReference(alarmController, "alarmAudioSource") == null)
            {
                Warn("Fire alarm has no AudioSource; light and emissive alarm feedback remain available.");
            }
        }

        private static void AuditSmoke(
            SmokeController smokeController,
            EmergencyStateManager stateManager)
        {
            if (smokeController == null)
            {
                return;
            }

            SerializedObject serializedSmoke = new SerializedObject(smokeController);
            SerializedProperty systems = serializedSmoke.FindProperty("smokeParticleSystems");
            Check(systems != null && systems.arraySize > 0,
                "SmokeController has intended ParticleSystems assigned.");
            Check(!smokeController.SmokeIsActive, "Smoke starts inactive.");
            Check(ReadReference(smokeController, "emergencyStateManager") == stateManager,
                "SmokeController references the EmergencyStateManager.");

            if (systems == null)
            {
                return;
            }

            for (int index = 0; index < systems.arraySize; index++)
            {
                ParticleSystem particleSystem =
                    systems.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
                Check(particleSystem != null, $"Smoke ParticleSystem {index} is assigned.");
                if (particleSystem != null)
                {
                    Check(!particleSystem.main.playOnAwake,
                        $"Smoke ParticleSystem {particleSystem.name} does not play on awake.");
                    Check(!particleSystem.isPlaying,
                        $"Smoke ParticleSystem {particleSystem.name} starts stopped.");
                }
            }
        }

        private static void AuditEvacuation(
            EvacuationManager evacuationManager,
            EvacuationWaypoint[] waypoints,
            AssemblyPointTrigger[] assemblyTriggers,
            EmergencyStateManager stateManager,
            List<GameObject> gameObjects)
        {
            if (evacuationManager == null)
            {
                return;
            }

            SerializedObject serializedManager = new SerializedObject(evacuationManager);
            Collider playerCollider =
                ReadReference(evacuationManager, "expectedPlayerCollider") as Collider;
            Transform assemblyPoint =
                ReadReference(evacuationManager, "assemblyPoint") as Transform;
            SerializedProperty orderedWaypoints = serializedManager.FindProperty("orderedWaypoints");

            Check(ReadReference(evacuationManager, "emergencyStateManager") == stateManager,
                "EvacuationManager references the EmergencyStateManager.");
            Check(playerCollider != null, "EvacuationManager references the player collider.");
            Check(assemblyPoint != null, "EvacuationManager references the assembly point.");
            Check(orderedWaypoints != null && orderedWaypoints.arraySize == 2,
                "Evacuation route contains Passage then Stairs.");

            if (orderedWaypoints != null)
            {
                for (int index = 0; index < orderedWaypoints.arraySize; index++)
                {
                    Transform waypointTransform =
                        orderedWaypoints.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    string expectedName = index == 0 ? "Passage" : "Stairs";
                    EvacuationWaypoint waypoint = waypointTransform != null
                        ? waypointTransform.GetComponent<EvacuationWaypoint>()
                        : null;

                    Check(waypointTransform != null && waypointTransform.name == expectedName,
                        $"Waypoint {index} is {expectedName} in orderedWaypoints.");
                    Check(waypoint != null && waypoint.WaypointIndex == index,
                        $"{expectedName} has waypoint index {index}.");
                    if (waypoint != null)
                    {
                        Check(ReadReference(waypoint, "evacuationManager") == evacuationManager,
                            $"{expectedName} references EvacuationManager.");
                        Check(ReadReference(waypoint, "expectedPlayerCollider") == playerCollider,
                            $"{expectedName} references the player collider.");
                        Collider triggerCollider = waypoint.GetComponent<Collider>();
                        Check(triggerCollider != null && triggerCollider.isTrigger,
                            $"{expectedName} has a trigger collider.");
                    }
                }
            }

            Check(waypoints.Length == 2, $"Exactly two evacuation waypoint components exist (found {waypoints.Length}).");
            Check(assemblyPoint != null &&
                  assemblyPoint.GetComponent<AssemblyPointTrigger>() != null,
                "Assembly point has an AssemblyPointTrigger.");

            AssemblyPointTrigger assembly = assemblyPoint != null
                ? assemblyPoint.GetComponent<AssemblyPointTrigger>()
                : null;
            if (assembly != null)
            {
                Check(assemblyTriggers.Contains(assembly), "AssemblyPointTrigger exists in the test scene.");
                Check(ReadReference(assembly, "emergencyStateManager") == stateManager,
                    "AssemblyPointTrigger references EmergencyStateManager.");
                Check(ReadReference(assembly, "evacuationManager") == evacuationManager,
                    "AssemblyPointTrigger references EvacuationManager.");
                Check(ReadReference(assembly, "expectedPlayerCollider") == playerCollider,
                    "AssemblyPointTrigger references the player collider.");
                Collider trigger = assembly.GetComponent<Collider>();
                Check(trigger != null && trigger.isTrigger, "Assembly point has a trigger collider.");
            }
            else
            {
                Fail("Assembly point is missing.");
            }

            Check(gameObjects.Any(gameObject => gameObject.name == "Passage") &&
                  gameObjects.Any(gameObject => gameObject.name == "Stairs"),
                "Passage and Stairs markers exist in the scene.");
        }

        private static void AuditDoor(
            EmergencyDoorController[] doorControllers,
            EmergencyStateManager stateManager,
            XRInteractionManager interactionManager)
        {
            Check(doorControllers.Length == 1,
                $"Exactly one EmergencyDoorController exists (found {doorControllers.Length}).");
            EmergencyDoorController door = doorControllers.FirstOrDefault();
            if (door == null)
            {
                return;
            }

            Check(ReadReference(door, "emergencyStateManager") == stateManager,
                "EmergencyDoorController references EmergencyStateManager.");
            XRSimpleInteractable interactable =
                ReadReference(door, "doorInteractable") as XRSimpleInteractable;
            Check(interactable != null, "EmergencyDoorController references an XR Simple Interactable.");
            Check(ReadReference(door, "doorLeafPivot") != null,
                "EmergencyDoorController references its door leaf pivot.");
            Check(ReadReference(door, "accessIndicator") != null,
                "EmergencyDoorController references its access indicator.");
            if (interactable != null)
            {
                Check(ReadReference(interactable, "m_InteractionManager") == interactionManager,
                    "Emergency door uses the scene XRInteractionManager.");
                Check(!interactable.enabled, "Emergency door starts interaction-locked.");
            }
        }

        private static void AuditExtinguisher(
            FireExtinguisherController[] extinguishers,
            EmergencyStateManager stateManager,
            FireController fireController,
            FireSuppressionTarget[] fireTargets)
        {
            Check(extinguishers.Length == 1,
                $"Exactly one FireExtinguisherController exists (found {extinguishers.Length}).");
            FireExtinguisherController extinguisher = extinguishers.FirstOrDefault();
            if (extinguisher == null)
            {
                return;
            }

            Check(ReadReference(extinguisher, "emergencyStateManager") == stateManager,
                "FireExtinguisherController references EmergencyStateManager.");
            Check(ReadReference(extinguisher, "fireController") == fireController,
                "FireExtinguisherController references FireController.");
            Check(ReadReference(extinguisher, "grabInteractable") != null,
                "FireExtinguisherController references its grab interactable.");
            Check(ReadReference(extinguisher, "nozzle") != null,
                "FireExtinguisherController references its nozzle.");
            Check(ReadReference(extinguisher, "suppressionEffect") != null,
                "FireExtinguisherController references its suppression effect.");
            SerializedProperty targets = new SerializedObject(extinguisher).FindProperty("targets");
            Check(targets != null && targets.arraySize == fireTargets.Length,
                "FireExtinguisherController is configured for every fire target.");

            if (targets != null)
            {
                for (int index = 0; index < targets.arraySize; index++)
                {
                    FireSuppressionTarget target =
                        targets.GetArrayElementAtIndex(index).objectReferenceValue as FireSuppressionTarget;
                    Check(target != null && fireTargets.Contains(target),
                        $"Extinguisher target {index} is a configured fire target.");
                }
            }
        }

        private static void AuditStatusUi(
            EvacuationStatusUI[] statusUis,
            EmergencyStateManager stateManager,
            EvacuationManager evacuationManager)
        {
            Check(statusUis.Length == 1,
                $"Exactly one EvacuationStatusUI exists (found {statusUis.Length}).");
            EvacuationStatusUI statusUi = statusUis.FirstOrDefault();
            if (statusUi == null)
            {
                return;
            }

            Check(ReadReference(statusUi, "emergencyStateManager") == stateManager,
                "EvacuationStatusUI references EmergencyStateManager.");
            Check(ReadReference(statusUi, "evacuationManager") == evacuationManager,
                "EvacuationStatusUI references EvacuationManager.");
            Check(ReadReference(statusUi, "statusText") is TMP_Text,
                "EvacuationStatusUI references its TMP status text.");
        }

        private static T[] GetComponents<T>(IEnumerable<GameObject> gameObjects)
            where T : Component
        {
            return gameObjects
                .SelectMany(gameObject => gameObject.GetComponents<T>())
                .ToArray();
        }

        private static UnityEngine.Object ReadReference(Component component, string propertyName)
        {
            if (component == null)
            {
                return null;
            }

            SerializedProperty property =
                new SerializedObject(component).FindProperty(propertyName);
            return property != null ? property.objectReferenceValue : null;
        }

        private static bool HasAssignedArray(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize == 0)
            {
                return false;
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasArrayReference(
            Component component,
            string propertyName,
            UnityEngine.Object target)
        {
            if (component == null || target == null)
            {
                return false;
            }

            SerializedProperty property =
                new SerializedObject(component).FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return false;
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Check(bool condition, string message)
        {
            if (condition)
            {
                Pass(message);
            }
            else
            {
                Fail(message);
            }
        }

        private static void Pass(string message)
        {
            passes++;
            Debug.Log($"RESQ Integration PASS: {message}");
        }

        private static void Warn(string message)
        {
            warnings.Add(message);
            Debug.LogWarning($"RESQ Integration WARNING: {message}");
        }

        private static void Fail(string message)
        {
            failures.Add(message);
            Debug.LogError($"RESQ Integration FAIL: {message}");
        }

        private static bool LogSummary()
        {
            string summary =
                $"RESQ Phase 5 & 6 audit: {passes} PASS, {warnings.Count} WARNING, {failures.Count} FAIL.";
            if (failures.Count == 0)
            {
                Debug.Log(summary);
                return true;
            }

            Debug.LogError(summary);
            return false;
        }
    }
}
#endif
