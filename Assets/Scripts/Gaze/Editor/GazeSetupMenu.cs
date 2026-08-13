#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class GazeSetupMenu
{
    const string MenuRoot = "Escape Room/";

    [MenuItem(MenuRoot + "Setup Gaze System On Main Camera")]
    static void SetupGazeSystem()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            EditorUtility.DisplayDialog("Gaze Setup", "No Main Camera found in the scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(mainCamera.gameObject, "Setup Gaze System");

        if (mainCamera.GetComponent<GazeController>() == null)
        {
            mainCamera.gameObject.AddComponent<GazeController>();
        }

        if (mainCamera.GetComponent<GazeReticleUI>() == null)
        {
            mainCamera.gameObject.AddComponent<GazeReticleUI>();
        }

        PlayerLocomotion locomotion = mainCamera.GetComponentInParent<PlayerLocomotion>();
        if (locomotion == null)
        {
            Transform cameraTransform = mainCamera.transform;
            Transform previousParent = cameraTransform.parent;

            GameObject rigObject = new GameObject("PlayerRig");
            Undo.RegisterCreatedObjectUndo(rigObject, "Setup Gaze System");

            Vector3 cameraPosition = cameraTransform.position;
            rigObject.transform.position = new Vector3(cameraPosition.x, previousParent != null ? previousParent.position.y : 0f,
                cameraPosition.z);

            if (previousParent != null)
            {
                rigObject.transform.SetParent(previousParent, true);
            }

            cameraTransform.SetParent(rigObject.transform, true);
            locomotion = Undo.AddComponent<PlayerLocomotion>(rigObject);
        }

        if (mainCamera.GetComponent<EditorGazeLookTest>() == null)
        {
            Undo.AddComponent<EditorGazeLookTest>(mainCamera.gameObject);
        }

        EditorUtility.DisplayDialog("Gaze Setup",
            "Gaze system added to Main Camera.\n\nNext step: use 'Create Gaze Test Objects' to try it in Play Mode.\nHold right mouse button to look around in the Editor.",
            "OK");
    }

    [MenuItem(MenuRoot + "Create Gaze Test Objects")]
    static void CreateGazeTestObjects()
    {
        CreateInteractableTestCube(new Vector3(0f, 1.2f, 3f));
        CreateTeleportNode(new Vector3(-1.5f, 0.05f, 2f), "TeleportNode_A");
        CreateTeleportNode(new Vector3(1.5f, 0.05f, 2f), "TeleportNode_B");

        EditorUtility.DisplayDialog("Gaze Test Objects",
            "Created one interactable cube and two teleport nodes.\n\n" +
            "- Look at the cube for ~1.75s to interact.\n" +
            "- Look at a blue floor disc for ~1.5s to teleport.\n" +
            "- Hold right mouse button to look around while testing in the Editor.",
            "OK");
    }

    static void CreateInteractableTestCube(Vector3 position)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(cube, "Create Gaze Test Objects");
        cube.name = "Interactable_TestCube";
        cube.transform.position = position;
        cube.layer = LayerMask.NameToLayer(GazeLayers.Interactable);

        GazeInteractable interactable = Undo.AddComponent<GazeInteractable>(cube);
        GazeInteractableDebugAction debugAction = Undo.AddComponent<GazeInteractableDebugAction>(cube);

        UnityEditor.Events.UnityEventTools.AddPersistentListener(interactable.OnInteract, debugAction.Execute);
    }

    static void CreateTeleportNode(Vector3 position, string nodeName)
    {
        GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(node, "Create Gaze Test Objects");
        node.name = nodeName;
        node.transform.position = position;
        node.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
        node.layer = LayerMask.NameToLayer(GazeLayers.TeleportNode);

        Undo.AddComponent<TeleportNode>(node);
    }
}
#endif
