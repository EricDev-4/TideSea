using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TideSeaShopCollision
{
    const string PrefabPath = "Assets/03.Prefabs/Blender/seaside_shop.prefab";
    static TideSeaShopCollision() { EditorApplication.update += CheckRequest; }
    static void CheckRequest()
    {
        if (!File.Exists("Temp/TideSeaShopCollision.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || File.Exists("Temp/TideSeaCoastalLayout.restore")) return;
        File.Delete("Temp/TideSeaShopCollision.request");
        try { Build(); }
        catch (Exception e) { File.WriteAllText("Temp/TideSeaShopCollision.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("TideSea/Build Shop Interior Colliders")]
    public static void Build()
    {
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        var report = new StringBuilder();
        try
        {
            // A box around the entire building fills its interior and blocks the entrance.
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in prefab.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
                report.AppendLine($"Mesh collider: {filter.name} localBounds={filter.sharedMesh.bounds}");
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        var scene = SceneManager.GetActiveScene();
        report.AppendLine("Scene=" + scene.path);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(filter.gameObject);
            if (outer == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(outer) != PrefabPath) continue;
            // Existing rigidbody overrides remain kinematic; collider shape is inherited from the prefab.
            report.AppendLine($"Scene mesh: {filter.name} worldBounds={filter.GetComponent<Renderer>()?.bounds}");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        ValidateEntrance(report);
        report.AppendLine("OK prefab saved; building-wide box removed; non-convex mesh colliders preserve openings.");
        File.WriteAllText("Temp/TideSeaShopCollision.result", report.ToString());
    }

    static void ValidateEntrance(StringBuilder report)
    {
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerBehavior>();
        var controller = player != null ? player.GetComponent<CharacterController>() : null;
        float radius = controller != null ? controller.radius * Mathf.Max(player.transform.lossyScale.x, player.transform.lossyScale.z) : 0.3f;
        float height = controller != null ? controller.height * player.transform.lossyScale.y : 1.8f;
        Transform shop = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>())
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == PrefabPath) { shop = t; break; }
        if (shop == null) throw new InvalidOperationException("Shop scene instance missing.");
        var activeScene = SceneManager.GetActiveScene();
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), testScene);
            // Test far from the game world so unrelated scene colliders cannot affect the result.
            clone.transform.SetPositionAndRotation(new Vector3(10000, 10000, 10000), Quaternion.identity);
            clone.transform.localScale = shop.lossyScale;
            var renderers = clone.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var physics = testScene.GetPhysicsScene();
            Physics.SyncTransforms();
            int clear = 0; float firstX = 0;
            float foot = bounds.min.y + 0.25f;
            float startZ = bounds.max.z + radius + 0.2f;
            float endZ = bounds.center.z;
            for (float x = bounds.min.x + radius + 0.05f; x < bounds.max.x - radius - 0.05f; x += 0.1f)
            {
                var bottom = new Vector3(x, foot + radius, startZ);
                var top = new Vector3(x, foot + height - radius, startZ);
                if (!physics.CapsuleCast(bottom, top, radius, Vector3.back, out _, startZ - endZ, ~0, QueryTriggerInteraction.Ignore))
                { if (clear == 0) firstX = x; clear++; }
            }
            report.AppendLine($"Player capsule: radius={radius:F2} height={height:F2}; clear front-to-interior paths={clear}; first path localX={(firstX - 10000) / shop.lossyScale.x:F2}");
            if (clear == 0) throw new InvalidOperationException("No player-sized entrance path found; shop needs a doorway adjustment.");
            // A horizontal ray against the back wall must still hit building geometry.
            bool wallHit = physics.Raycast(new Vector3(10000, foot + height * 0.5f, bounds.min.z - 1), Vector3.forward, out _, bounds.size.z + 2);
            report.AppendLine("Back wall collision=" + wallHit);
            if (!wallHit) throw new InvalidOperationException("Back wall collision missing.");
        }
        finally { EditorSceneManager.CloseScene(testScene, true); SceneManager.SetActiveScene(activeScene); }
    }
}
