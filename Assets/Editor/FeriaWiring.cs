using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Ayudas de Editor para cablear las pistas (HintDirector) y otros eventos de
/// la feria en cada escena por código, en vez de arrastrar a mano en el
/// Inspector. Todo es idempotente: volver a correrlo quita las llamadas viejas
/// al mismo destino antes de agregarlas. No guarda la escena (queda dirty).
/// </summary>
public static class FeriaWiring
{
    public const string DirectorName = "Pistas (HintDirector)";
    private const BindingFlags AnyField = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    // ---------- búsquedas ----------

    public static GameObject Find(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            Transform t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    public static GameObject Need(string name)
    {
        GameObject go = Find(name);
        if (go == null) throw new Exception("[FeriaWiring] No encontré '" + name + "' en la escena.");
        return go;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            if (c.name == name) return c;
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    public static T FindComp<T>() where T : Component
    {
        foreach (T c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.gameObject.scene == SceneManager.GetActiveScene()) return c;
        return null;
    }

    // ---------- texto de pista en VR ----------

    /// <summary>Devuelve el VRHintText de la escena; si no hay, crea "Hint (VR)" bajo Main Camera.</summary>
    public static VRHintText EnsureHintText()
    {
        VRHintText existing = FindComp<VRHintText>();
        if (existing != null) return existing;

        Camera cam = Camera.main != null ? Camera.main : FindComp<Camera>();
        if (cam == null) throw new Exception("[FeriaWiring] La escena no tiene cámara.");

        GameObject go = new GameObject("Hint (VR)");
        Undo.RegisterCreatedObjectUndo(go, "Hint (VR)");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, -0.32f, 1.6f);
        go.layer = cam.gameObject.layer;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SpecialElite SDF.asset");
        Material overlay = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/SpecialElite SDF Overlay.mat");
        if (font != null) tmp.font = font;
        if (overlay != null) tmp.fontSharedMaterial = overlay;
        tmp.fontSize = 1f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.93f, 0.9f, 0.82f, 0f);
        tmp.text = "";
        tmp.rectTransform.sizeDelta = new Vector2(2.2f, 0.6f);
        tmp.enableWordWrapping = true;

        VRHintText hint = go.AddComponent<VRHintText>();
        SetField(hint, "_text", tmp);
        return hint;
    }

    // ---------- director ----------

    /// <summary>Crea (o reutiliza) el HintDirector de la escena y le borra los objetivos.</summary>
    public static HintDirector ResetDirector(VRHintText hint)
    {
        GameObject go = Find(DirectorName);
        if (go == null)
        {
            go = new GameObject(DirectorName);
            Undo.RegisterCreatedObjectUndo(go, DirectorName);
        }
        HintDirector d = go.GetComponent<HintDirector>();
        if (d == null) d = Undo.AddComponent<HintDirector>(go);
        Undo.RecordObject(d, "HintDirector");
        SetField(d, "_hint", hint);
        GetObjectives(d).Clear();
        return d;
    }

    public static List<HintDirector.Objective> GetObjectives(HintDirector d)
    {
        return (List<HintDirector.Objective>)GetField(d, "_objectives");
    }

    public static HintDirector.Objective AddObjective(HintDirector d, string label, string voiceText)
    {
        var o = new HintDirector.Objective { label = label, voiceText = voiceText };
        GetObjectives(d).Add(o);
        EditorUtility.SetDirty(d);
        return o;
    }

    // ---------- UnityEvents ----------

    /// <summary>Lee un UnityEvent (o UnityEvent&lt;T&gt;) privado serializado de un componente.</summary>
    public static UnityEventBase Event(Component c, string field)
    {
        FieldInfo f = FindField(c, field);
        var e = f.GetValue(c) as UnityEventBase;
        if (e == null && typeof(UnityEventBase).IsAssignableFrom(f.FieldType) && !f.FieldType.IsAbstract)
        {
            // Componente recién agregado: Unity todavía no creó el evento.
            e = (UnityEventBase)Activator.CreateInstance(f.FieldType);
            f.SetValue(c, e);
        }
        if (e == null) throw new Exception("[FeriaWiring] " + c.GetType().Name + "." + field + " está vacío o no es UnityEvent.");
        return e;
    }

    /// <summary>Quita todas las llamadas persistentes de <paramref name="e"/> a <paramref name="target"/> con ese método.</summary>
    public static void RemoveCalls(UnityEventBase e, UnityEngine.Object target, string method)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method)
                UnityEventTools.RemovePersistentListener(e, i);
    }

    // Quita las llamadas viejas al mismo método y agrega la nueva (idempotente).
    private static void Wire<T>(UnityEventBase e, UnityEngine.Object target, string method, Action<T> add) where T : Delegate
    {
        RemoveCalls(e, target, method);
        add((T)Delegate.CreateDelegate(typeof(T), target, method));
    }

    public static void Void(UnityEventBase e, UnityEngine.Object target, string method)
    {
        Wire<UnityAction>(e, target, method, d => UnityEventTools.AddVoidPersistentListener(e, d));
    }

    public static void Bool(UnityEventBase e, UnityEngine.Object target, string method, bool value)
    {
        Wire<UnityAction<bool>>(e, target, method, d => UnityEventTools.AddBoolPersistentListener(e, d, value));
    }

    public static void Str(UnityEventBase e, UnityEngine.Object target, string method, string value)
    {
        Wire<UnityAction<string>>(e, target, method, d => UnityEventTools.AddStringPersistentListener(e, d, value));
    }

    public static void Int(UnityEventBase e, UnityEngine.Object target, string method, int value)
    {
        Wire<UnityAction<int>>(e, target, method, d => UnityEventTools.AddIntPersistentListener(e, d, value));
    }

    public static void Float(UnityEventBase e, UnityEngine.Object target, string method, float value)
    {
        Wire<UnityAction<float>>(e, target, method, d => UnityEventTools.AddFloatPersistentListener(e, d, value));
    }

    /// <summary>GameObject.SetActive(value) como llamada persistente.</summary>
    public static void SetActive(UnityEventBase e, GameObject go, bool value)
    {
        Bool(e, go, "SetActive", value);
    }

    public static void Dirty(params UnityEngine.Object[] objs)
    {
        foreach (var o in objs) if (o != null) EditorUtility.SetDirty(o);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ---------- componentes ----------

    public static HintGlow EnsureGlow(GameObject go, Color color, float strength = 0.8f)
    {
        HintGlow g = go.GetComponent<HintGlow>();
        if (g == null) g = Undo.AddComponent<HintGlow>(go);
        SetField(g, "_color", color);
        SetField(g, "_strength", strength);
        return g;
    }

    // Busca el campo (privado o público) subiendo por la jerarquía de tipos.
    private static FieldInfo FindField(UnityEngine.Object o, string field)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(field, AnyField);
            if (f != null) return f;
        }
        throw new Exception("[FeriaWiring] " + o.GetType().Name + " no tiene el campo " + field);
    }

    public static void SetField(UnityEngine.Object o, string field, object value)
    {
        FindField(o, field).SetValue(o, value);
        EditorUtility.SetDirty(o);
    }

    public static object GetField(UnityEngine.Object o, string field)
    {
        return FindField(o, field).GetValue(o);
    }
}
