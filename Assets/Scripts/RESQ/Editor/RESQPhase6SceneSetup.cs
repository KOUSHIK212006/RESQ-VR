#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using RESQ;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RESQ.EditorTools
{
    internal static class RESQPhase6SceneSetup
    {
        private const string TestScenePath = "Assets/Scenes/RESQ_Phase5_Test.unity";
        private const string Phase6RootName = "RESQ_Phase6";
        private const string PrefabFolder = "Assets/_RESQ/Prefabs/Phase6";
        private const string MaterialFolder = "Assets/_RESQ/Materials/Phase6";
        private const string ExtinguisherPrefabPath = PrefabFolder + "/RESQ_FireExtinguisher_Temporary.prefab";
        private const string DoorPrefabPath = PrefabFolder + "/RESQ_EmergencyDoor_Temporary.prefab";
        private const string SprayMaterialPath = MaterialFolder + "/RESQ_Suppression_Spray.mat";
        private const string ExtinguisherRedMaterialPath = MaterialFolder + "/RESQ_Extinguisher_Red.mat";
        private const string ExtinguisherDarkMaterialPath = MaterialFolder + "/RESQ_Extinguisher_Dark.mat";
        private const string ExtinguisherLabelMaterialPath = MaterialFolder + "/RESQ_Extinguisher_Label.mat";
        private const string DoorMaterialPath = MaterialFolder + "/RESQ_EmergencyDoor.mat";
        private const string DoorFrameMaterialPath = MaterialFolder + "/RESQ_EmergencyDoor_Frame.mat";
        private const string DoorIndicatorMaterialPath = MaterialFolder + "/RESQ_EmergencyDoor_Indicator.mat";

        [MenuItem("RESQ/Phase 6/1. Create Extinguisher #&F9")]
        private static void CreateExtinguisher()
        {
            if (!TryGetTestScene(out Scene scene) ||
                !TryGetPhase5References(scene, out GameObject emergencyRoot, out EmergencyStateManager stateManager, out FireController fireController) ||
                !TryGetPlayerAndEnvironment(scene, out PlayerEnvironment playerEnvironment))
            {
                return;
            }

            GameObject phase6Root = FindInRoots(scene.GetRootGameObjects(), Phase6RootName);
            if (phase6Root != null && FindInHierarchy(phase6Root.transform, "Fire Extinguisher") != null)
            {
                Debug.LogError("Phase 6 setup stopped because a fire extinguisher already exists in the test scene.");
                return;
            }

            FireSuppressionTarget[] targets = emergencyRoot.GetComponentsInChildren<FireSuppressionTarget>(true);
            XRInteractionManager interactionManager = FindInSceneComponent<XRInteractionManager>(scene);
            if (targets.Length == 0)
            {
                Debug.LogError("Phase 6 setup stopped because no Phase 5 FireSuppressionTarget exists.", emergencyRoot);
                return;
            }

            try
            {
                EnsureFolder(PrefabFolder);
                EnsureFolder(MaterialFolder);
                Material redMaterial = GetOrCreateMaterial(
                    ExtinguisherRedMaterialPath,
                    new Color(0.75f, 0.025f, 0.02f, 1f));
                Material darkMaterial = GetOrCreateMaterial(
                    ExtinguisherDarkMaterialPath,
                    new Color(0.035f, 0.045f, 0.055f, 1f));
                Material labelMaterial = GetOrCreateMaterial(
                    ExtinguisherLabelMaterialPath,
                    new Color(0.95f, 0.72f, 0.08f, 1f));
                Material sprayMaterial = GetOrCreateMaterial(
                    SprayMaterialPath,
                    new Color(0.88f, 0.94f, 1f, 0.85f),
                    true);

                GameObject prefab = CreateExtinguisherPrefab(
                    redMaterial,
                    darkMaterial,
                    labelMaterial,
                    sprayMaterial);
                if (prefab == null)
                {
                    return;
                }

                phase6Root = phase6Root != null ? phase6Root : CreateSceneRoot(scene, Phase6RootName);
                if (interactionManager == null)
                {
                    interactionManager = Undo.AddComponent<XRInteractionManager>(phase6Root);
                    Debug.LogWarning(
                        "No XRInteractionManager was present in the test scene; a shared manager was added under RESQ_Phase6.",
                        phase6Root);
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException("Could not instantiate the temporary extinguisher prefab.");
                }

                Undo.RegisterCreatedObjectUndo(instance, "Create temporary fire extinguisher");
                Undo.SetTransformParent(instance.transform, phase6Root.transform, "Parent fire extinguisher");
                Undo.RecordObject(instance.transform, "Place temporary fire extinguisher");
                instance.name = "Fire Extinguisher";
                instance.transform.position = playerEnvironment.camera.transform.position +
                                              playerEnvironment.routeDirection * 0.65f;
                instance.transform.position = new Vector3(
                    instance.transform.position.x,
                    playerEnvironment.floorY + 0.38f,
                    instance.transform.position.z);
                instance.transform.rotation = Quaternion.LookRotation(
                    playerEnvironment.routeDirection,
                    Vector3.up);

                for (int index = 0; index < targets.Length; index++)
                {
                    EnsureSuppressionCollider(targets[index]);
                }

                FireExtinguisherController extinguisher =
                    instance.GetComponent<FireExtinguisherController>();
                XRGrabInteractable grabInteractable = instance.GetComponent<XRGrabInteractable>();
                SetObjectReference(grabInteractable, "m_InteractionManager", interactionManager);

                SerializedObject serializedExtinguisher = new SerializedObject(extinguisher);
                Undo.RecordObject(extinguisher, "Wire fire extinguisher");
                SetObjectReference(serializedExtinguisher, "grabInteractable", grabInteractable);
                SetObjectReference(serializedExtinguisher, "emergencyStateManager", stateManager);
                SetObjectReference(serializedExtinguisher, "fireController", fireController);
                SetObjectArray(serializedExtinguisher, "targets", Array.ConvertAll(targets, target => (UnityEngine.Object)target));
                serializedExtinguisher.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(extinguisher);

                SaveScene(scene, "Phase 6 extinguisher integration saved.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool HasObjectReference(Component component, string propertyName)
        {
            if (component == null)
            {
                return false;
            }

            SerializedProperty property =
                new SerializedObject(component).FindProperty(propertyName);
            return property != null && property.objectReferenceValue != null;
        }

        [MenuItem("RESQ/Phase 6/2. Create Door and Evacuation Route #&F10")]
        private static void CreateDoorAndRoute()
        {
            if (!TryGetTestScene(out Scene scene) ||
                !TryGetPhase5References(scene, out _, out EmergencyStateManager stateManager, out _) ||
                !TryGetPlayerAndEnvironment(scene, out PlayerEnvironment playerEnvironment))
            {
                return;
            }

            GameObject phase6Root = FindInRoots(scene.GetRootGameObjects(), Phase6RootName);
            if (phase6Root != null &&
                (FindInHierarchy(phase6Root.transform, "Emergency Door") != null ||
                 FindInHierarchy(phase6Root.transform, "Evacuation Manager") != null ||
                 FindInHierarchy(phase6Root.transform, "Passage") != null ||
                 FindInHierarchy(phase6Root.transform, "Stairs") != null ||
                 FindInHierarchy(phase6Root.transform, "Basement Assembly Area Marker") != null))
            {
                Debug.LogError("Phase 6 route setup stopped because a door or evacuation manager already exists.");
                return;
            }

            XRInteractionManager interactionManager = FindInSceneComponent<XRInteractionManager>(scene);
            if (interactionManager == null)
            {
                Debug.LogError(
                    "Create the Phase 6 extinguisher first. Its setup creates or locates the shared XRInteractionManager.");
                return;
            }

            try
            {
                EnsureFolder(PrefabFolder);
                EnsureFolder(MaterialFolder);
                Material doorMaterial = GetOrCreateMaterial(
                    DoorMaterialPath,
                    new Color(0.12f, 0.19f, 0.27f, 1f));
                Material frameMaterial = GetOrCreateMaterial(
                    DoorFrameMaterialPath,
                    new Color(0.55f, 0.59f, 0.64f, 1f));
                Material indicatorMaterial = GetOrCreateMaterial(
                    DoorIndicatorMaterialPath,
                    new Color(1f, 0.45f, 0.05f, 1f));

                GameObject doorPrefab = CreateDoorPrefab(
                    doorMaterial,
                    frameMaterial,
                    indicatorMaterial);
                if (doorPrefab == null)
                {
                    return;
                }

                phase6Root = phase6Root != null ? phase6Root : CreateSceneRoot(scene, Phase6RootName);
                GameObject door = PrefabUtility.InstantiatePrefab(doorPrefab, scene) as GameObject;
                if (door == null)
                {
                    throw new InvalidOperationException("Could not instantiate the temporary emergency door prefab.");
                }

                Undo.RegisterCreatedObjectUndo(door, "Create temporary emergency door");
                Undo.SetTransformParent(door.transform, phase6Root.transform, "Parent emergency door");
                Undo.RecordObject(door.transform, "Place emergency door");
                door.name = "Emergency Door";
                door.transform.position = playerEnvironment.RoutePoint(0.23f);
                door.transform.rotation = Quaternion.LookRotation(playerEnvironment.routeDirection, Vector3.up);

                EmergencyDoorController doorController = door.GetComponent<EmergencyDoorController>();
                XRBaseInteractable doorInteractable =
                    door.GetComponentInChildren<XRSimpleInteractable>(true);
                SerializedObject serializedDoor = new SerializedObject(doorController);
                Undo.RecordObject(doorController, "Wire emergency door");
                SetObjectReference(serializedDoor, "emergencyStateManager", stateManager);
                serializedDoor.ApplyModifiedProperties();
                SetObjectReference(
                    doorInteractable,
                    "m_InteractionManager",
                    interactionManager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(doorController);

                GameObject evacuationObject = CreateSceneChild(
                    scene,
                    phase6Root.transform,
                    "Evacuation Manager");
                EvacuationManager evacuationManager =
                    Undo.AddComponent<EvacuationManager>(evacuationObject);
                GameObject passage = CreateWaypoint(
                    scene,
                    phase6Root.transform,
                    "Passage",
                    "PASSAGE\nTRAINING MARKER",
                    playerEnvironment.RoutePoint(0.48f),
                    0);
                GameObject stairs = CreateWaypoint(
                    scene,
                    phase6Root.transform,
                    "Stairs",
                    "STAIRS\nTRAINING MARKER",
                    playerEnvironment.RoutePoint(0.72f),
                    1);
                GameObject assembly = CreateAssemblyPoint(
                    scene,
                    phase6Root.transform,
                    "Basement Assembly Area Marker",
                    "ASSEMBLY POINT\nTRAINING MARKER",
                    playerEnvironment.RoutePoint(0.94f));
                CreateEmergencyControls(
                    scene,
                    phase6Root.transform,
                    stateManager,
                    interactionManager,
                    playerEnvironment);

                Collider playerCollider = playerEnvironment.playerCollider;
                EvacuationWaypoint[] waypointComponents =
                {
                    passage.GetComponent<EvacuationWaypoint>(),
                    stairs.GetComponent<EvacuationWaypoint>()
                };
                for (int index = 0; index < waypointComponents.Length; index++)
                {
                    SerializedObject serializedWaypoint = new SerializedObject(waypointComponents[index]);
                    Undo.RecordObject(waypointComponents[index], "Wire evacuation waypoint");
                    serializedWaypoint.FindProperty("waypointIndex").intValue = index;
                    SetObjectReference(serializedWaypoint, "evacuationManager", evacuationManager);
                    SetObjectReference(serializedWaypoint, "expectedPlayerCollider", playerCollider);
                    serializedWaypoint.ApplyModifiedProperties();
                }

                AssemblyPointTrigger assemblyTrigger =
                    assembly.GetComponent<AssemblyPointTrigger>();
                SerializedObject serializedAssembly = new SerializedObject(assemblyTrigger);
                Undo.RecordObject(assemblyTrigger, "Wire assembly point");
                SetObjectReference(serializedAssembly, "emergencyStateManager", stateManager);
                SetObjectReference(serializedAssembly, "evacuationManager", evacuationManager);
                SetObjectReference(serializedAssembly, "expectedPlayerCollider", playerCollider);
                serializedAssembly.ApplyModifiedProperties();

                SerializedObject serializedManager = new SerializedObject(evacuationManager);
                Undo.RecordObject(evacuationManager, "Wire evacuation manager");
                SetObjectReference(serializedManager, "emergencyStateManager", stateManager);
                SetObjectReference(serializedManager, "expectedPlayerCollider", playerCollider);
                SetObjectArray(
                    serializedManager,
                    "orderedWaypoints",
                    new UnityEngine.Object[] { passage.transform, stairs.transform });
                SetObjectReference(serializedManager, "assemblyPoint", assembly.transform);
                serializedManager.ApplyModifiedProperties();

                SaveScene(
                    scene,
                    $"Temporary door and training route saved. The route length is {playerEnvironment.routeLength:F1} m; no physical stairs or basement were created.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("RESQ/Phase 6/3. Create Evacuation Status UI #&F11")]
        private static void CreateEvacuationUi()
        {
            if (!TryGetTestScene(out Scene scene))
            {
                return;
            }

            GameObject phase6Root = FindInRoots(scene.GetRootGameObjects(), Phase6RootName);
            if (phase6Root == null)
            {
                Debug.LogError("Phase 6 UI setup stopped because RESQ_Phase6 is missing.");
                return;
            }

            if (FindInHierarchy(phase6Root.transform, "Evacuation Status UI") != null)
            {
                Debug.LogError("Phase 6 UI setup stopped because the status panel already exists.");
                return;
            }

            if (!TryGetPhase5References(scene, out _, out EmergencyStateManager stateManager, out _) ||
                !TryGetPlayerAndEnvironment(scene, out PlayerEnvironment playerEnvironment))
            {
                return;
            }

            EvacuationManager evacuationManager =
                FindInSceneComponent<EvacuationManager>(scene);
            if (evacuationManager == null)
            {
                Debug.LogError("Create the Phase 6 door and route before creating the status panel.");
                return;
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                string[] fontGuids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
                if (fontGuids.Length > 0)
                {
                    font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                        AssetDatabase.GUIDToAssetPath(fontGuids[0]));
                }
            }

            if (font == null)
            {
                Debug.LogError("Phase 6 UI setup stopped because no TextMesh Pro font asset is available.");
                return;
            }

            GameObject panel = new GameObject("Evacuation Status UI", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(panel, scene);
            Undo.RegisterCreatedObjectUndo(panel, "Create evacuation status panel");
            Undo.SetTransformParent(panel.transform, phase6Root.transform, "Parent evacuation status panel");
            Canvas canvas = Undo.AddComponent<Canvas>(panel);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = playerEnvironment.camera;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(panel);
            scaler.dynamicPixelsPerUnit = 10f;
            Undo.AddComponent<GraphicRaycaster>(panel);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Undo.RecordObject(panelRect, "Size evacuation status panel");
            panelRect.sizeDelta = new Vector2(1000f, 600f);
            panelRect.localScale = Vector3.one * 0.0016f;

            Image background = Undo.AddComponent<Image>(panel);
            background.color = new Color(0.025f, 0.055f, 0.09f, 0.94f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Status Text", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(textObject, scene);
            Undo.RegisterCreatedObjectUndo(textObject, "Create evacuation status text");
            Undo.SetTransformParent(textObject.transform, panel.transform, "Parent evacuation status text");
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(42f, 32f);
            textRect.offsetMax = new Vector2(-42f, -32f);

            TextMeshProUGUI statusText = Undo.AddComponent<TextMeshProUGUI>(textObject);
            statusText.font = font;
            statusText.fontSize = 34f;
            statusText.color = Color.white;
            statusText.alignment = TextAlignmentOptions.TopLeft;
            statusText.textWrappingMode = TextWrappingModes.Normal;
            statusText.raycastTarget = false;

            EvacuationStatusUI statusUi = Undo.AddComponent<EvacuationStatusUI>(panel);
            SerializedObject serializedUi = new SerializedObject(statusUi);
            Undo.RecordObject(statusUi, "Wire evacuation status panel");
            SetObjectReference(serializedUi, "emergencyStateManager", stateManager);
            SetObjectReference(serializedUi, "evacuationManager", evacuationManager);
            SetObjectReference(serializedUi, "statusText", statusText);
            serializedUi.ApplyModifiedProperties();

            Vector3 panelPosition = playerEnvironment.camera.transform.position +
                                    playerEnvironment.routeDirection * 2.3f +
                                    Vector3.up * 0.55f;
            Undo.RecordObject(panel.transform, "Place evacuation status panel");
            panel.transform.position = panelPosition;
            panel.transform.rotation = Quaternion.LookRotation(
                playerEnvironment.camera.transform.position - panelPosition,
                Vector3.up);

            SaveScene(scene, "Phase 6 evacuation status UI saved.");
        }

        [MenuItem("RESQ/Phase 6/Validate Test Scene")]
        private static void ValidateTestScene()
        {
            if (!TryGetTestScene(out Scene scene))
            {
                return;
            }

            List<string> errors = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects();
            if (FindInRoots(roots, "RESQ_Emergency") == null)
            {
                errors.Add("RESQ_Emergency / Phase 5 hierarchy is missing.");
            }

            GameObject phase6Root = FindInRoots(roots, Phase6RootName);
            if (phase6Root == null)
            {
                errors.Add("RESQ_Phase6 hierarchy is missing.");
            }
            else
            {
                if (FindInHierarchy(phase6Root.transform, "Fire Extinguisher") == null)
                {
                    errors.Add("Temporary fire extinguisher is missing.");
                }

                if (FindInHierarchy(phase6Root.transform, "Emergency Door") == null)
                {
                    errors.Add("Emergency door is missing.");
                }

                if (FindInHierarchy(phase6Root.transform, "Evacuation Manager") == null)
                {
                    errors.Add("Evacuation manager is missing.");
                }

                if (FindInHierarchy(phase6Root.transform, "Evacuation Status UI") == null)
                {
                    errors.Add("Evacuation status panel is missing.");
                }

                if (FindInHierarchy(phase6Root.transform, "Emergency Controls") == null)
                {
                    errors.Add("Emergency start/evacuate/reset controls are missing.");
                }
            }

            if (!TryGetPlayerAndEnvironment(scene, out _))
            {
                errors.Add("XR Origin camera/collider or environment bounds could not be resolved.");
            }

            if (errors.Count == 0)
            {
                FireExtinguisherController extinguisher =
                    FindInSceneComponent<FireExtinguisherController>(scene);
                EmergencyDoorController door =
                    FindInSceneComponent<EmergencyDoorController>(scene);
                EvacuationManager evacuation =
                    FindInSceneComponent<EvacuationManager>(scene);
                EvacuationStatusUI statusUi =
                    FindInSceneComponent<EvacuationStatusUI>(scene);
                AssemblyPointTrigger assembly =
                    FindInSceneComponent<AssemblyPointTrigger>(scene);
                if (extinguisher == null ||
                    !HasObjectReference(extinguisher, "fireController") ||
                    !HasObjectReference(extinguisher, "emergencyStateManager") ||
                    !HasObjectReference(extinguisher, "nozzle") ||
                    !HasObjectReference(extinguisher, "suppressionEffect"))
                {
                    errors.Add("Extinguisher serialized references are incomplete.");
                }

                if (door == null ||
                    !HasObjectReference(door, "emergencyStateManager") ||
                    !HasObjectReference(door, "doorInteractable") ||
                    !HasObjectReference(door, "doorLeafPivot"))
                {
                    errors.Add("Emergency door serialized references are incomplete.");
                }

                if (evacuation == null ||
                    !HasObjectReference(evacuation, "emergencyStateManager") ||
                    !HasObjectReference(evacuation, "expectedPlayerCollider") ||
                    !HasObjectReference(evacuation, "assemblyPoint"))
                {
                    errors.Add("Evacuation manager serialized references are incomplete.");
                }

                if (statusUi == null ||
                    !HasObjectReference(statusUi, "emergencyStateManager") ||
                    !HasObjectReference(statusUi, "evacuationManager") ||
                    !HasObjectReference(statusUi, "statusText"))
                {
                    errors.Add("Evacuation UI serialized references are incomplete.");
                }

                if (assembly == null ||
                    !HasObjectReference(assembly, "emergencyStateManager") ||
                    !HasObjectReference(assembly, "evacuationManager") ||
                    !HasObjectReference(assembly, "expectedPlayerCollider"))
                {
                    errors.Add("Assembly point serialized references are incomplete.");
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log(
                    "Phase 6 Edit Mode validation passed: Phase 5 and Phase 6 objects are present. This does not verify Play Mode behavior.",
                    phase6Root);
            }
            else
            {
                Debug.LogError(
                    "Phase 6 Edit Mode validation found: " + string.Join(" ", errors));
            }
        }

        private static GameObject CreateExtinguisherPrefab(
            Material redMaterial,
            Material darkMaterial,
            Material labelMaterial,
            Material sprayMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ExtinguisherPrefabPath);
            if (existing != null)
            {
                if (existing.GetComponent<XRGrabInteractable>() != null &&
                    existing.GetComponent<FireExtinguisherController>() != null)
                {
                    return existing;
                }

                Debug.LogError(
                    $"A prefab already exists at {ExtinguisherPrefabPath}, but it is not the expected extinguisher. It was not overwritten.");
                return null;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("RESQ_FireExtinguisher_Temporary");
                SceneManager.MoveGameObjectToScene(root, previewScene);

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                CapsuleCollider bodyCollider = root.AddComponent<CapsuleCollider>();
                bodyCollider.radius = 0.105f;
                bodyCollider.height = 0.62f;
                bodyCollider.direction = 1;

                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cylinder,
                    "Red Cylinder Body",
                    root.transform,
                    Vector3.zero,
                    Vector3.zero,
                    new Vector3(0.2f, 0.31f, 0.2f),
                    redMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cylinder,
                    "Neck Collar",
                    root.transform,
                    new Vector3(0f, 0.31f, 0f),
                    Vector3.zero,
                    new Vector3(0.13f, 0.045f, 0.13f),
                    darkMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cylinder,
                    "Base Ring",
                    root.transform,
                    new Vector3(0f, -0.29f, 0f),
                    Vector3.zero,
                    new Vector3(0.205f, 0.035f, 0.205f),
                    darkMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cube,
                    "Yellow Instruction Band",
                    root.transform,
                    new Vector3(0f, 0.08f, 0.095f),
                    Vector3.zero,
                    new Vector3(0.13f, 0.17f, 0.018f),
                    labelMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cube,
                    "Black Trigger Handle",
                    root.transform,
                    new Vector3(0f, 0.38f, 0f),
                    Vector3.zero,
                    new Vector3(0.22f, 0.045f, 0.12f),
                    darkMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cylinder,
                    "Nozzle Barrel",
                    root.transform,
                    new Vector3(0f, 0.27f, 0.17f),
                    new Vector3(90f, 0f, 0f),
                    new Vector3(0.055f, 0.16f, 0.055f),
                    darkMaterial,
                    false);

                GameObject nozzleObject = new GameObject("Nozzle");
                SceneManager.MoveGameObjectToScene(nozzleObject, previewScene);
                nozzleObject.transform.SetParent(root.transform, false);
                nozzleObject.transform.localPosition = new Vector3(0f, 0.27f, 0.27f);
                nozzleObject.transform.localRotation = Quaternion.identity;

                GameObject sprayObject = new GameObject("Suppression Spray");
                SceneManager.MoveGameObjectToScene(sprayObject, previewScene);
                sprayObject.transform.SetParent(nozzleObject.transform, false);
                sprayObject.transform.localPosition = Vector3.zero;
                ParticleSystem spray = sprayObject.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = spray.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = 0.32f;
                main.startSpeed = 5f;
                main.startSize = 0.055f;
                main.startColor = new Color(0.88f, 0.94f, 1f, 0.85f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                ParticleSystem.EmissionModule emission = spray.emission;
                emission.rateOverTime = 90f;
                ParticleSystem.ShapeModule shape = spray.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 7f;
                shape.radius = 0.018f;
                ParticleSystemRenderer sprayRenderer = sprayObject.GetComponent<ParticleSystemRenderer>();
                sprayRenderer.sharedMaterial = sprayMaterial;
                sprayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sprayRenderer.receiveShadows = false;
                spray.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
                SerializedObject serializedGrab = new SerializedObject(grab);
                SetObjectArray(serializedGrab, "m_Colliders", new UnityEngine.Object[] { bodyCollider });
                serializedGrab.ApplyModifiedPropertiesWithoutUndo();

                FireExtinguisherController controller =
                    root.AddComponent<FireExtinguisherController>();
                SerializedObject serializedController = new SerializedObject(controller);
                SetObjectReference(serializedController, "grabInteractable", grab);
                SetObjectReference(serializedController, "nozzle", nozzleObject.transform);
                SetObjectReference(serializedController, "suppressionEffect", spray);
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                return SavePrefabAsset(root, ExtinguisherPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static GameObject CreateDoorPrefab(
            Material doorMaterial,
            Material frameMaterial,
            Material indicatorMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            if (existing != null)
            {
                if (existing.GetComponent<EmergencyDoorController>() != null &&
                    existing.GetComponentInChildren<XRSimpleInteractable>(true) != null)
                {
                    return existing;
                }

                Debug.LogError(
                    $"A prefab already exists at {DoorPrefabPath}, but it is not the expected door. It was not overwritten.");
                return null;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("RESQ_EmergencyDoor_Temporary");
                SceneManager.MoveGameObjectToScene(root, previewScene);

                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cube,
                    "Left Door Frame",
                    root.transform,
                    new Vector3(-0.53f, 1.08f, 0f),
                    Vector3.zero,
                    new Vector3(0.12f, 2.16f, 0.18f),
                    frameMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cube,
                    "Right Door Frame",
                    root.transform,
                    new Vector3(0.53f, 1.08f, 0f),
                    Vector3.zero,
                    new Vector3(0.12f, 2.16f, 0.18f),
                    frameMaterial,
                    false);
                CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cube,
                    "Door Header",
                    root.transform,
                    new Vector3(0f, 2.16f, 0f),
                    Vector3.zero,
                    new Vector3(1.18f, 0.12f, 0.18f),
                    frameMaterial,
                    false);

                GameObject pivot = new GameObject("Door Leaf Pivot");
                SceneManager.MoveGameObjectToScene(pivot, previewScene);
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(-0.47f, 0f, 0f);

                GameObject leaf = CreatePrimitive(
                    previewScene,
                    PrimitiveType.Cube,
                    "Door Leaf",
                    pivot.transform,
                    new Vector3(0.47f, 1.05f, 0f),
                    Vector3.zero,
                    new Vector3(0.92f, 2.1f, 0.1f),
                    doorMaterial,
                    true);

                GameObject handle = CreatePrimitive(
                    previewScene,
                    PrimitiveType.Sphere,
                    "Interactable Handle",
                    pivot.transform,
                    new Vector3(0.8f, 1.05f, -0.08f),
                    Vector3.zero,
                    Vector3.one * 0.11f,
                    indicatorMaterial,
                    true);
                SphereCollider handleCollider = handle.GetComponent<SphereCollider>();
                XRSimpleInteractable interactable = handle.AddComponent<XRSimpleInteractable>();
                SerializedObject serializedInteractable = new SerializedObject(interactable);
                SetObjectArray(
                    serializedInteractable,
                    "m_Colliders",
                    new UnityEngine.Object[] { handleCollider });
                serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

                EmergencyDoorController doorController =
                    root.AddComponent<EmergencyDoorController>();
                SerializedObject serializedDoor = new SerializedObject(doorController);
                SetObjectReference(serializedDoor, "doorInteractable", interactable);
                SetObjectReference(serializedDoor, "doorLeafPivot", pivot.transform);
                SetObjectReference(
                    serializedDoor,
                    "accessIndicator",
                    handle.GetComponent<Renderer>());
                serializedDoor.ApplyModifiedPropertiesWithoutUndo();

                return SavePrefabAsset(root, DoorPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static GameObject CreateWaypoint(
            Scene scene,
            Transform parent,
            string name,
            string label,
            Vector3 position,
            int index)
        {
            GameObject waypoint = CreateSceneChild(scene, parent, name);
            waypoint.transform.position = position;

            SphereCollider trigger = Undo.AddComponent<SphereCollider>(waypoint);
            trigger.isTrigger = true;
            trigger.radius = 0.4f;
            Rigidbody body = Undo.AddComponent<Rigidbody>(waypoint);
            body.isKinematic = true;
            body.useGravity = false;

            EvacuationWaypoint component = Undo.AddComponent<EvacuationWaypoint>(waypoint);
            SerializedObject serializedWaypoint = new SerializedObject(component);
            serializedWaypoint.FindProperty("waypointIndex").intValue = index;
            serializedWaypoint.ApplyModifiedProperties();

            CreateWaypointVisual(scene, waypoint.transform, label, new Color(0.08f, 0.62f, 0.95f, 1f));
            return waypoint;
        }

        private static GameObject CreateAssemblyPoint(
            Scene scene,
            Transform parent,
            string name,
            string label,
            Vector3 position)
        {
            GameObject assembly = CreateSceneChild(scene, parent, name);
            assembly.transform.position = position;

            SphereCollider trigger = Undo.AddComponent<SphereCollider>(assembly);
            trigger.isTrigger = true;
            trigger.radius = 0.72f;
            Rigidbody body = Undo.AddComponent<Rigidbody>(assembly);
            body.isKinematic = true;
            body.useGravity = false;
            Undo.AddComponent<AssemblyPointTrigger>(assembly);

            CreateWaypointVisual(scene, assembly.transform, label, new Color(0.1f, 0.82f, 0.35f, 1f));
            return assembly;
        }

        private static void CreateEmergencyControls(
            Scene scene,
            Transform parent,
            EmergencyStateManager stateManager,
            XRInteractionManager interactionManager,
            PlayerEnvironment playerEnvironment)
        {
            GameObject controls = CreateSceneChild(scene, parent, "Emergency Controls");
            Vector3 right = Vector3.Cross(Vector3.up, playerEnvironment.routeDirection).normalized;
            Vector3 panelPosition = playerEnvironment.RoutePoint(0.08f) + right * 0.72f;
            panelPosition.y = playerEnvironment.floorY;
            controls.transform.position = panelPosition;
            controls.transform.rotation = Quaternion.LookRotation(
                playerEnvironment.camera.transform.position - panelPosition,
                Vector3.up);

            CreateControlButton(
                scene,
                controls.transform,
                "Start Emergency",
                "START\nALARM",
                new Vector3(0f, 0.92f, 0f),
                new Color(0.85f, 0.08f, 0.04f, 1f),
                EmergencyStateControl.Action.StartEmergency,
                stateManager,
                interactionManager);
            CreateControlButton(
                scene,
                controls.transform,
                "Begin Evacuation",
                "BEGIN\nEVACUATION",
                new Vector3(0f, 0.6f, 0f),
                new Color(0.95f, 0.55f, 0.05f, 1f),
                EmergencyStateControl.Action.BeginEvacuation,
                stateManager,
                interactionManager);
            CreateControlButton(
                scene,
                controls.transform,
                "Reset Emergency",
                "RESET",
                new Vector3(0f, 0.28f, 0f),
                new Color(0.25f, 0.32f, 0.4f, 1f),
                EmergencyStateControl.Action.ResetEmergency,
                stateManager,
                interactionManager);
        }

        private static void CreateControlButton(
            Scene scene,
            Transform parent,
            string name,
            string label,
            Vector3 localPosition,
            Color color,
            EmergencyStateControl.Action action,
            EmergencyStateManager stateManager,
            XRInteractionManager interactionManager)
        {
            Material material = GetOrCreateMaterial(
                MaterialFolder + "/RESQ_Control_" + action + ".mat",
                color,
                true);
            GameObject button = CreatePrimitive(
                scene,
                PrimitiveType.Cube,
                name,
                parent,
                localPosition,
                Vector3.zero,
                new Vector3(0.42f, 0.24f, 0.12f),
                material,
                true);
            XRSimpleInteractable interactable = Undo.AddComponent<XRSimpleInteractable>(button);
            SerializedObject serializedInteractable = new SerializedObject(interactable);
            SetObjectReference(serializedInteractable, "m_InteractionManager", interactionManager);
            SetObjectArray(
                serializedInteractable,
                "m_Colliders",
                new UnityEngine.Object[] { button.GetComponent<BoxCollider>() });
            serializedInteractable.ApplyModifiedProperties();

            EmergencyStateControl control = Undo.AddComponent<EmergencyStateControl>(button);
            SerializedObject serializedControl = new SerializedObject(control);
            SetObjectReference(serializedControl, "emergencyStateManager", stateManager);
            SetObjectReference(serializedControl, "interactable", interactable);
            serializedControl.FindProperty("action").enumValueIndex = (int)action;
            serializedControl.ApplyModifiedProperties();

            GameObject labelObject = new GameObject("Button Label");
            SceneManager.MoveGameObjectToScene(labelObject, scene);
            Undo.RegisterCreatedObjectUndo(labelObject, "Create emergency button label");
            Undo.SetTransformParent(labelObject.transform, button.transform, "Parent emergency button label");
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.56f);
            labelObject.transform.localRotation = Quaternion.identity;
            TextMesh text = Undo.AddComponent<TextMesh>(labelObject);
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.01f;
            text.color = Color.white;
        }

        private static void CreateWaypointVisual(
            Scene scene,
            Transform parent,
            string label,
            Color color)
        {
            Material markerMaterial = GetOrCreateMaterial(
                MaterialFolder + "/RESQ_Evacuation_Marker_" +
                (label.StartsWith("ASSEMBLY", StringComparison.Ordinal) ? "Green" : "Blue") +
                ".mat",
                color,
                true);
            GameObject marker = CreatePrimitive(
                scene,
                PrimitiveType.Cylinder,
                "Visible Route Marker",
                parent,
                new Vector3(0f, -0.08f, 0f),
                Vector3.zero,
                new Vector3(0.55f, 0.035f, 0.55f),
                markerMaterial,
                false);
            marker.transform.localPosition = new Vector3(0f, -0.08f, 0f);

            GameObject labelObject = new GameObject("Route Label");
            SceneManager.MoveGameObjectToScene(labelObject, scene);
            Undo.RegisterCreatedObjectUndo(labelObject, "Create route label");
            Undo.SetTransformParent(labelObject.transform, parent, "Parent route label");
            labelObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            TextMesh text = Undo.AddComponent<TextMesh>(labelObject);
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.04f;
            text.color = Color.white;
        }

        private static void EnsureSuppressionCollider(FireSuppressionTarget target)
        {
            if (target == null)
            {
                return;
            }

            SphereCollider collider = target.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<SphereCollider>(target.gameObject);
            }

            Undo.RecordObject(collider, "Add fire suppression target collider");
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.radius = 0.65f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        private static GameObject CreatePrimitive(
            Scene scene,
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            SceneManager.MoveGameObjectToScene(primitive, scene);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = Quaternion.Euler(localEulerAngles);
            primitive.transform.localScale = localScale;

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = primitive.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            return primitive;
        }

        private static GameObject CreateSceneRoot(Scene scene, string name)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            return root;
        }

        private static GameObject CreateSceneChild(Scene scene, Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, scene);
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            Undo.SetTransformParent(child.transform, parent, $"Parent {name}");
            return child;
        }

        private static GameObject SavePrefabAsset(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError($"Could not create project-owned Phase 6 prefab at {path}.");
                return null;
            }

            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static Material GetOrCreateMaterial(string path, Color color, bool emission = false)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible Unity Lit shader is available.");
            }

            Material material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 0.6f);
                material.EnableKeyword("_EMISSION");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static bool TryGetTestScene(out Scene scene)
        {
            scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Phase 6 setup is Edit Mode only. No changes were made.");
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded || scene.path != TestScenePath)
            {
                Debug.LogError(
                    $"Phase 6 setup is restricted to the loaded {TestScenePath}. Active scene: {scene.path}");
                return false;
            }

            return true;
        }

        private static bool TryGetPhase5References(
            Scene scene,
            out GameObject emergencyRoot,
            out EmergencyStateManager stateManager,
            out FireController fireController)
        {
            emergencyRoot = FindInRoots(scene.GetRootGameObjects(), "RESQ_Emergency");
            stateManager = emergencyRoot != null
                ? emergencyRoot.GetComponent<EmergencyStateManager>()
                : null;
            fireController = emergencyRoot != null
                ? emergencyRoot.GetComponentInChildren<FireController>(true)
                : null;

            if (emergencyRoot == null || stateManager == null || fireController == null)
            {
                Debug.LogError(
                    "Phase 6 setup stopped because RESQ_Emergency, EmergencyStateManager, or FireController is missing.");
                return false;
            }

            return true;
        }

        private static bool TryGetPlayerAndEnvironment(
            Scene scene,
            out PlayerEnvironment playerEnvironment)
        {
            playerEnvironment = default;
            GameObject[] roots = scene.GetRootGameObjects();
            Camera playerCamera = null;
            Collider playerCollider = null;

            foreach (GameObject root in roots)
            {
                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                for (int index = 0; index < cameras.Length; index++)
                {
                    if (cameras[index].CompareTag("MainCamera"))
                    {
                        playerCamera = cameras[index];
                        playerCollider = cameras[index].GetComponentInParent<CharacterController>();
                        if (playerCollider == null)
                        {
                            playerCollider = cameras[index].GetComponentInParent<Collider>();
                        }

                        break;
                    }
                }

                if (playerCamera != null)
                {
                    break;
                }
            }

            GameObject environment = FindInRoots(roots, "Environment");
            GameObject templateEnvironment = environment != null
                ? FindInHierarchy(environment.transform, "Template Environment")
                : null;

            if (playerCamera == null ||
                playerCollider == null ||
                templateEnvironment == null ||
                !TryGetBounds(templateEnvironment.GetComponentsInChildren<Renderer>(true), out Bounds bounds))
            {
                Debug.LogError(
                    "Phase 6 setup stopped because the MainCamera, its player collider, or Template Environment bounds could not be resolved.");
                return false;
            }

            Vector3 routeDirection = FindRouteDirection(
                playerCamera.transform.position,
                bounds,
                templateEnvironment.transform);
            float routeLength = GetFloorClearance(
                playerCamera.transform.position,
                routeDirection,
                bounds);
            routeLength = Mathf.Min(4.2f, routeLength - 0.45f);

            if (routeLength < 1.5f)
            {
                Debug.LogError(
                    "Phase 6 setup stopped because no clear training route of at least 1.5 m fits inside the existing environment.");
                return false;
            }

            playerEnvironment = new PlayerEnvironment
            {
                camera = playerCamera,
                playerCollider = playerCollider,
                templateEnvironment = templateEnvironment,
                bounds = bounds,
                floorY = bounds.min.y + 0.02f,
                routeDirection = routeDirection,
                routeLength = routeLength
            };
            return true;
        }

        private static Vector3 FindRouteDirection(
            Vector3 playerPosition,
            Bounds bounds,
            Transform environmentRoot)
        {
            Vector3[] candidates =
            {
                Vector3.forward,
                Vector3.back,
                Vector3.right,
                Vector3.left,
                (Vector3.forward + Vector3.right).normalized,
                (Vector3.forward + Vector3.left).normalized,
                (Vector3.back + Vector3.right).normalized,
                (Vector3.back + Vector3.left).normalized
            };

            Vector3 bestDirection = Vector3.forward;
            float bestClearance = -1f;
            foreach (Vector3 candidate in candidates)
            {
                float clearance = GetFloorClearance(playerPosition, candidate, bounds);
                Vector3 rayOrigin = new Vector3(
                    playerPosition.x,
                    bounds.min.y + 1f,
                    playerPosition.z);
                RaycastHit[] hits = Physics.RaycastAll(
                    rayOrigin,
                    candidate,
                    6f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider != null &&
                        hit.transform.IsChildOf(environmentRoot) &&
                        hit.distance > 0f)
                    {
                        clearance = Mathf.Min(clearance, hit.distance - 0.45f);
                    }
                }

                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestDirection = candidate;
                }
            }

            return bestDirection;
        }

        private static float GetFloorClearance(Vector3 position, Vector3 direction, Bounds bounds)
        {
            float distanceX = direction.x > 0.001f
                ? (bounds.max.x - 0.45f - position.x) / direction.x
                : direction.x < -0.001f
                    ? (bounds.min.x + 0.45f - position.x) / direction.x
                    : float.PositiveInfinity;
            float distanceZ = direction.z > 0.001f
                ? (bounds.max.z - 0.45f - position.z) / direction.z
                : direction.z < -0.001f
                    ? (bounds.min.z + 0.45f - position.z) / direction.z
                    : float.PositiveInfinity;

            return Mathf.Max(0f, Mathf.Min(distanceX, distanceZ));
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static T FindInSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
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

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized field {propertyName} was not found on {serializedObject.targetObject.name}.");
            }

            property.objectReferenceValue = value;
        }

        private static void SetObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            Undo.RecordObject(component, $"Assign {propertyName}");
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(
                    $"Serialized array {propertyName} was not found on {serializedObject.targetObject.name}.");
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SaveScene(Scene scene, string message)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"Failed to save {TestScenePath}.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(message, scene.GetRootGameObjects()[0]);
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

        private struct PlayerEnvironment
        {
            public Camera camera;
            public Collider playerCollider;
            public GameObject templateEnvironment;
            public Bounds bounds;
            public float floorY;
            public Vector3 routeDirection;
            public float routeLength;

            public Vector3 RoutePoint(float fraction)
            {
                Vector3 point = camera.transform.position +
                                routeDirection * (routeLength * fraction);
                point.y = floorY;
                return point;
            }
        }
    }
}
#endif
