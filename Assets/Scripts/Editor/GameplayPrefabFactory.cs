#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameplayPrefabFactory
{
    private const string PrefabFolder = "Assets/Prefabs/GDD";

    [MenuItem("Tools/Juego Plataforma/Setup Recommended Layers")]
    public static void SetupRecommendedLayers()
    {
        string[] layerNames =
        {
            "Player",
            "Ground",
            "Movable",
            "Interactable",
            "Hazard",
            "Collectible",
            "LaserBlocker",
            "PressureActivator",
            "WindAffected"
        };

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        foreach (string layerName in layerNames)
            EnsureLayer(layers, layerName);

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Juego Plataforma/Create GDD MVP Prefabs")]
    public static void CreateGddMvpPrefabs()
    {
        EnsurePrefabFolder();
        SetupRecommendedLayers();

        CreatePlayerPrefab();
        CreateBoxPrefab("Box_Normal", 10f, 1f, true, true, true);
        CreateBoxPrefab("Box_Heavy", 200f, 3f, false, true, true);
        CreateBoxPrefab("Box_Rail", 12f, 1f, true, true, true, true);
        CreatePressureButtonPrefab();
        CreateDoorPrefab();
        CreateMovingPlatformPrefab();
        CreateLaserEmitterPrefab();
        CreateFanZonePrefab();
        CreateLavaZonePrefab();
        CreateGearCollectiblePrefab();
        CreateCheckpointPrefab();
        CreateDeathZonePrefab();
        CreatePuzzleZonePrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Juego Plataforma/Setup Cinemachine Camera")]
    public static void SetupCinemachineCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        AddComponentByTypeName(mainCamera.gameObject, "Cinemachine.CinemachineBrain, Unity.Cinemachine");

        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("No se encontro PlayerController en la escena. Crea o selecciona un Player antes de configurar Cinemachine.");
            return;
        }

        Transform cameraTarget = player.transform.Find("CameraTarget");
        if (cameraTarget == null)
        {
            GameObject targetObject = new GameObject("CameraTarget");
            targetObject.transform.SetParent(player.transform);
            targetObject.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            targetObject.transform.localRotation = Quaternion.identity;
            cameraTarget = targetObject.transform;
        }

        Type cameraType = Type.GetType("Cinemachine.CinemachineCamera, Unity.Cinemachine");
        if (cameraType == null)
        {
            Debug.LogWarning("No se encontro CinemachineCamera. Verifica que el paquete Cinemachine este instalado.");
            return;
        }

        GameObject cinemachineObject = GameObject.Find("Cinemachine Camera") ?? new GameObject("Cinemachine Camera");
        Component cinemachineCamera = cinemachineObject.GetComponent(cameraType) ?? cinemachineObject.AddComponent(cameraType);
        TryAssignTarget(cinemachineCamera, "Follow", cameraTarget);
        TryAssignTarget(cinemachineCamera, "LookAt", cameraTarget);

        Selection.activeGameObject = cinemachineObject;
        Debug.Log("Camara Cinemachine configurada para seguir CameraTarget.");
    }

    private static void CreatePlayerPrefab()
    {
        GameObject root = new GameObject("Player");
        SetLayerIfExists(root, "Player");

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        CapsuleCollider controllerCollider = root.AddComponent<CapsuleCollider>();
        controllerCollider.height = 2f;
        controllerCollider.radius = 0.35f;
        controllerCollider.center = new Vector3(0f, 1f, 0f);

        root.AddComponent<PlayerAbilities>();
        root.AddComponent<PlayerInventory>();
        root.AddComponent<PlayerRespawn>();
        root.AddComponent<PlayerInteraction>();
        root.AddComponent<PlayerMotor>();
        root.AddComponent<PlayerController>();

        WeightedObject weightedObject = root.AddComponent<WeightedObject>();
        weightedObject.pressureWeight = 1f;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

        GameObject cameraTarget = new GameObject("CameraTarget");
        cameraTarget.transform.SetParent(root.transform);
        cameraTarget.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        SaveAndDestroy(root, $"{PrefabFolder}/Player.prefab");
    }

    private static void CreateBoxPrefab(string name, float physicsMass, float pressureWeight, bool canBeGrabbed, bool canBePushed, bool blocksLaser, bool useRailMovable = false)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        SetLayerIfExists(box, "Movable");

        Rigidbody rb = box.AddComponent<Rigidbody>();
        rb.mass = physicsMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        MovableObject movable = useRailMovable ? box.AddComponent<RailMovableObject>() : box.AddComponent<MovableObject>();
        movable.canBeGrabbed = canBeGrabbed;
        movable.canBePushed = canBePushed;
        movable.physicsMass = physicsMass;
        movable.pressureWeight = pressureWeight;
        movable.canBlockLaser = blocksLaser;
        movable.useRuntimePhysicsMaterial = true;

        WeightedObject weightedObject = box.AddComponent<WeightedObject>();
        weightedObject.pressureWeight = pressureWeight;

        box.AddComponent<ResettableTransform>();

        SaveAndDestroy(box, $"{PrefabFolder}/{name}.prefab");
    }

    private static void CreatePressureButtonPrefab()
    {
        GameObject root = new GameObject("PressureButton");
        SetLayerIfExists(root, "PressureActivator");

        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.4f, 0.25f, 1.4f);
        trigger.center = new Vector3(0f, 0.15f, 0f);

        PressureButton button = root.AddComponent<PressureButton>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "ButtonVisual";
        visual.transform.SetParent(root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(1f, 0.15f, 1f);
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
        button.buttonVisual = visual.transform;

        SaveAndDestroy(root, $"{PrefabFolder}/PressureButton.prefab");
    }

    private static void CreateDoorPrefab()
    {
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.localScale = new Vector3(2f, 3f, 0.3f);
        DoorActivator activator = door.AddComponent<DoorActivator>();
        activator.doorVisual = door.transform;
        door.AddComponent<ResettableTransform>();
        SaveAndDestroy(door, $"{PrefabFolder}/Door.prefab");
    }

    private static void CreateMovingPlatformPrefab()
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "MovingPlatform";
        platform.transform.localScale = new Vector3(3f, 0.25f, 3f);
        Rigidbody rb = platform.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        platform.AddComponent<MovingPlatform>();
        platform.AddComponent<ResettableTransform>();
        SaveAndDestroy(platform, $"{PrefabFolder}/MovingPlatform.prefab");
    }

    private static void CreateLaserEmitterPrefab()
    {
        GameObject laser = new GameObject("LaserEmitter");
        LineRenderer lineRenderer = laser.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;

        GameObject origin = new GameObject("Origin");
        origin.transform.SetParent(laser.transform);
        origin.transform.localPosition = Vector3.zero;
        origin.transform.localRotation = Quaternion.identity;

        LaserEmitter emitter = laser.AddComponent<LaserEmitter>();
        emitter.origin = origin.transform;
        emitter.directionReference = origin.transform;
        emitter.lineRenderer = lineRenderer;

        SaveAndDestroy(laser, $"{PrefabFolder}/LaserEmitter.prefab");
    }

    private static void CreateFanZonePrefab()
    {
        GameObject fan = new GameObject("FanZone");
        SetLayerIfExists(fan, "Hazard");
        BoxCollider trigger = fan.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 3f, 5f);
        fan.AddComponent<FanZone>();
        SaveAndDestroy(fan, $"{PrefabFolder}/FanZone.prefab");
    }

    private static void CreateLavaZonePrefab()
    {
        GameObject lava = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lava.name = "LavaZone";
        SetLayerIfExists(lava, "Hazard");
        lava.transform.localScale = new Vector3(5f, 0.2f, 5f);
        BoxCollider collider = lava.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        lava.AddComponent<LavaZone>();
        SaveAndDestroy(lava, $"{PrefabFolder}/LavaZone.prefab");
    }

    private static void CreateGearCollectiblePrefab()
    {
        GameObject gear = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gear.name = "GearCollectible";
        SetLayerIfExists(gear, "Collectible");
        SphereCollider collider = gear.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        gear.transform.localScale = Vector3.one * 0.5f;
        gear.AddComponent<GearCollectible>();
        SaveAndDestroy(gear, $"{PrefabFolder}/GearCollectible.prefab");
    }

    private static void CreateCheckpointPrefab()
    {
        GameObject checkpoint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        checkpoint.name = "Checkpoint";
        checkpoint.transform.localScale = new Vector3(1.2f, 0.1f, 1.2f);
        CapsuleCollider collider = checkpoint.GetComponent<CapsuleCollider>();
        collider.isTrigger = true;
        checkpoint.AddComponent<Checkpoint>();
        SaveAndDestroy(checkpoint, $"{PrefabFolder}/Checkpoint.prefab");
    }

    private static void CreateDeathZonePrefab()
    {
        GameObject deathZone = new GameObject("DeathZone");
        SetLayerIfExists(deathZone, "Hazard");
        BoxCollider collider = deathZone.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(10f, 1f, 10f);
        deathZone.AddComponent<DeathZone>();
        SaveAndDestroy(deathZone, $"{PrefabFolder}/DeathZone.prefab");
    }

    private static void CreatePuzzleZonePrefab()
    {
        GameObject puzzleZone = new GameObject("PuzzleZone");
        puzzleZone.AddComponent<PuzzleZone>();
        SaveAndDestroy(puzzleZone, $"{PrefabFolder}/PuzzleZone.prefab");
    }

    private static void EnsurePrefabFolder()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "GDD");
    }

    private static void SetLayerIfExists(GameObject gameObject, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            gameObject.layer = layer;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void SaveAndDestroy(GameObject instance, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static void EnsureLayer(SerializedProperty layers, string layerName)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (layer.stringValue == layerName)
                return;
        }

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;

            layer.stringValue = layerName;
            return;
        }

        Debug.LogWarning($"No hay slots libres para crear la layer {layerName}.");
    }

    private static void AddComponentByTypeName(GameObject target, string typeName)
    {
        Type type = Type.GetType(typeName);
        if (type == null || target.GetComponent(type) != null)
            return;

        target.AddComponent(type);
    }

    private static void TryAssignTarget(Component component, string memberName, Transform target)
    {
        Type type = component.GetType();
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(typeof(Transform)))
        {
            property.SetValue(component, target);
            return;
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsAssignableFrom(typeof(Transform)))
            field.SetValue(component, target);
    }
}
#endif
