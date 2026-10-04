using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Existing prefabs remain linked. This tool changes scene instances only.
[InitializeOnLoad]
public static class TideSeaCoastalLayout
{
    const string RootName = "Coastal Life";
    const string RequestPath = "Temp/TideSeaCoastalLayout.request";
    const string ResizeRequestPath = "Temp/TideSeaCoastalLayout.resize";
    const string RestoreRequestPath = "Temp/TideSeaCoastalLayout.restore";
    static readonly Dictionary<string, ItemSO> Items = new Dictionary<string, ItemSO>();
    static readonly List<Vector3> Occupied = new List<Vector3>();
    static System.Random random;
    static Transform root;
    static PlayerBehavior player;
    static float water;
    static int count;

    static TideSeaCoastalLayout() { EditorApplication.update += CheckRequest; }
    static void CheckRequest()
    {
        if (File.Exists(RestoreRequestPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(RestoreRequestPath);
            try { Resize(0.5f, 0.4f, "Temp/TideSeaCoastalLayout.restore.result"); }
            catch (Exception e) { File.WriteAllText("Temp/TideSeaCoastalLayout.restore.result", e.ToString()); Debug.LogException(e); }
        }
        if (File.Exists(ResizeRequestPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(ResizeRequestPath);
            try { Enlarge(); }
            catch (Exception e) { File.WriteAllText("Temp/TideSeaCoastalLayout.resize.result", e.ToString()); Debug.LogException(e); }
        }
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        try { Arrange(); }
        catch (Exception e) { File.WriteAllText("Temp/TideSeaCoastalLayout.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("TideSea/Enlarge Existing Coastal Objects")]
    public static void Enlarge()
    {
        Resize(2.0f, 2.5f, "Temp/TideSeaCoastalLayout.resize.result");
    }

    static void Resize(float shopFactor, float objectFactor, string resultPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/TideSea_Base.unity") throw new InvalidOperationException("Open TideSea_Base first.");
        var layout = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        var ocean = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "StylizedWater3_OceanGrid");
        if (layout == null || ocean == null) throw new InvalidOperationException("Coastal layout or ocean missing.");
        water = ocean.transform.position.y;
        var shopGroup = layout.transform.Find("Shop and Work Area");
        var shopObject = shopGroup.Cast<Transform>().First(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject).name == "seaside_shop");
        Vector3 shopOrigin = shopObject.position;
        Directory.CreateDirectory("Temp/TideSeaSceneBackups");
        File.Copy(scene.path, "Temp/TideSeaSceneBackups/TideSea_Base_before_resize_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Enlarge Coastal Objects");
        int resized = 0;
        try
        {
            foreach (Transform group in layout.transform)
            foreach (Transform t in group)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null) continue;
                string name = source.name;
                bool fish = name.StartsWith("fish_") && name != "fish_crate" || name == "squid";
                bool boat = name == "rowboat";
                var oldBounds = BoundsOf(t.gameObject);
                Undo.RecordObject(t, "Enlarge " + name);
                t.localScale *= name == "seaside_shop" ? shopFactor : objectFactor;
                if (group == shopGroup && t != shopObject)
                {
                    Vector3 offset = t.position - shopOrigin;
                    t.position = shopOrigin + new Vector3(offset.x * shopFactor, offset.y, offset.z * shopFactor);
                }
                var bounds = BoundsOf(t.gameObject);
                if (boat)
                    t.position += Vector3.up * (water - bounds.min.y - bounds.size.y * 0.25f);
                else if (fish)
                {
                    float min = oldBounds.center.y;
                    if (Ground(t.position, out var bottom, out _)) min = bottom.y + bounds.extents.y + 0.1f;
                    float max = water - bounds.extents.y - 0.1f;
                    float center = min <= max ? Mathf.Clamp(oldBounds.center.y, min, max) : (min + max) * 0.5f;
                    t.position += Vector3.up * (center - bounds.center.y);
                }
                else
                {
                    float ground = oldBounds.min.y - 0.015f;
                    if (Ground(t.position, out var point, out _)) ground = point.y;
                    t.position += Vector3.up * (ground + 0.015f - bounds.min.y);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                resized++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            Undo.CollapseUndoOperations(undo);
            File.WriteAllText(resultPath, $"OK resized={resized} shopFactor={shopFactor} objectFactor={objectFactor} scene={scene.path}");
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    [MenuItem("TideSea/Arrange Coastal Life Around Player")]
    public static void Arrange()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before arranging the scene.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/TideSea_Base.unity") throw new InvalidOperationException("Open TideSea_Base first.");
        player = UnityEngine.Object.FindAnyObjectByType<PlayerBehavior>();
        if (player == null) throw new InvalidOperationException("PlayerBehavior not found.");
        var ocean = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "StylizedWater3_OceanGrid");
        if (ocean == null) throw new InvalidOperationException("Ocean surface not found.");
        water = ocean.transform.position.y;
        Items.Clear(); Occupied.Clear(); random = new System.Random(1042026); count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/04.SO/Items" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            Items[item.name] = item;
        }
        foreach (var item in Items.Values)
            if (item.prefab == null) throw new InvalidOperationException("Missing prefab: " + item.name);
        var dry = new List<Vector3>(); var beach = new List<Vector3>(); var submerged = new List<Vector3>();
        for (int x = -62; x <= 62; x += 2)
        for (int z = -62; z <= 62; z += 2)
        {
            if (x * x + z * z > 62 * 62 || x * x + z * z < 16) continue;
            if (!Ground(player.transform.position + new Vector3(x, 0, z), out var point, out var normal) || normal.y < 0.9f) continue;
            if (point.y >= water + 1.0f) dry.Add(point);
            else if (point.y >= water + 0.08f) beach.Add(point);
            else if (point.y < water - 1.2f && point.y > water - 12f) submerged.Add(point);
        }
        // Include the immediately submerged tidal margin for bottom-dwelling creatures.
        if (dry.Count == 0 || beach.Count == 0 || submerged.Count == 0)
            throw new InvalidOperationException($"Missing suitable terrain: dry={dry.Count}, beach={beach.Count}, underwater={submerged.Count}");
        var shop = dry.Where(p => Vector3.Distance(p, player.transform.position) > 9 && Vector3.Distance(p, player.transform.position) < 30)
            .OrderBy(p => ShopScore(p)).FirstOrDefault();
        if (shop == default || ShopScore(shop) > 1000) throw new InvalidOperationException("No sufficiently flat shop site near the player.");
        Directory.CreateDirectory("Temp/TideSeaSceneBackups");
        File.Copy(scene.path, "Temp/TideSeaSceneBackups/TideSea_Base_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Arrange Coastal Life");
        try
        {
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            var go = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(go, "Create coastal layout");
            SceneManager.MoveGameObjectToScene(go, scene); root = go.transform;
            var shopGroup = Group("Shop and Work Area");
            var beachGroup = Group("Beach Shells and Tide Life");
            var underwaterGroup = Group("Underwater Life");
            var shore = beach.OrderBy(p => (p - shop).sqrMagnitude).First();
            Vector3 forward = shore - shop; forward.y = 0; forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            PlaceGround("seaside_shop", shop, yaw, 6, shopGroup);
            ShopProp("market_display_table", -2.1f, 4.2f, 2.4f);
            ShopProp("display_shelf", -3.7f, 0.8f, 2.0f);
            ShopProp("shop_sign", 3.8f, 3.8f, 0.9f);
            ShopProp("storage_barrel", 3.5f, -1.8f, 0.9f);
            ShopProp("storage_chest", -3.8f, -1.8f, 1.2f);
            ShopProp("fish_crate", 1.0f, 4.2f, 1.0f);
            ShopProp("cooking_table", 6.5f, 0.0f, 2.0f);
            ShopProp("stone_grill", 6.7f, 2.7f, 1.3f);
            ShopProp("drying_rack", 5.8f, -2.8f, 2.2f);
            ShopProp("crafting_bench", -6.5f, -0.5f, 2.0f);
            ShopProp("gathering_basket", -1.0f, 3.7f, 0.6f);
            ShopProp("crab_trap", 2.5f, 4.6f, 0.9f);
            void ShopProp(string name, float x, float z, float width)
            {
                var point = shop + right * x + forward * z;
                if (Ground(point, out var hit, out _) && hit.y > water + 0.2f) PlaceGround(name, hit, yaw, width, shopGroup);
            }
            // Loose clusters with a clear route from the spawn to the shop and shoreline.
            Scatter(beach, new[] {"shell", "seashell", "shell_oyster", "shell_mussel", "shell_razor", "shell_pearl_oyster"}, 34, 0.16f, 0.34f, beachGroup, false);
            Scatter(beach, new[] {"crab", "starfish"}, 10, 0.25f, 0.50f, beachGroup, false);
            Scatter(beach, new[] {"driftwood", "old_bottle"}, 6, 0.25f, 0.70f, beachGroup, false);
            Scatter(submerged, new[] {"seaweed", "seaweed_red", "seaweed_branch", "seaweed_ribbon"}, 22, 0.5f, 1.1f, underwaterGroup, false);
            Scatter(submerged, new[] {"sea_urchin", "shrimp", "shell_oyster", "shell_pearl_oyster"}, 14, 0.22f, 0.48f, underwaterGroup, false);
            Scatter(submerged, new[] {"fish_round", "fish_flat", "fish_long", "fish_silver", "fish_coral", "squid"}, 24, 0.35f, 0.75f, underwaterGroup, true);
            // Boat just off the nearest sufficiently deep beach section.
            var boatPoint = submerged.OrderBy(p => (p - shore).sqrMagnitude).First();
            var boat = Spawn("rowboat", boatPoint, yaw + 15, 3.5f, beachGroup);
            Bounds boatBounds = BoundsOf(boat);
            boat.transform.position += Vector3.up * (water - boatBounds.min.y - boatBounds.size.y * 0.25f);
            Freeze(boat);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            File.WriteAllText("Temp/TideSeaCoastalLayout.result", $"OK scene={scene.path}\ncount={count}\nplayer={player.transform.position}\nwater={water}\nshop={shop}\nbeachSites={beach.Count}\nunderwaterSites={submerged.Count}\n");
            Debug.Log($"Coastal Life: placed {count} linked prefab instances around the player.");
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static Transform Group(string name)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create habitat group"); go.transform.SetParent(root); return go.transform;
    }
    static bool Ground(Vector3 position, out Vector3 point, out Vector3 normal)
    {
        var hits = Physics.RaycastAll(new Vector3(position.x, 200, position.z), Vector3.down, 500, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits.OrderBy(h => h.distance))
        {
            if (hit.collider.GetComponentInParent<PlayerBehavior>() != null || hit.collider.GetComponentInParent<Item>() != null || hit.collider.transform.root.name == RootName || hit.collider.gameObject.layer == 4) continue;
            if (!(hit.collider is TerrainCollider) && !(hit.collider is MeshCollider)) continue;
            point = hit.point; normal = hit.normal; return true;
        }
        point = default; normal = default; return false;
    }
    static float ShopScore(Vector3 p)
    {
        float min = p.y, max = p.y;
        for (int x = -7; x <= 7; x += 7)
        for (int z = -6; z <= 6; z += 6)
        {
            if (!Ground(p + new Vector3(x, 0, z), out var q, out var n) || q.y <= water + 0.4f || n.y < 0.93f) return 10000;
            min = Mathf.Min(min, q.y); max = Mathf.Max(max, q.y);
        }
        if (max - min > 1.5f) return 10000;
        return (max - min) * 15 + Vector3.Distance(p, player.transform.position);
    }
    static GameObject Spawn(string name, Vector3 position, float yaw, float width, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Items[name].prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Place " + name);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var b = BoundsOf(go); float current = Mathf.Max(b.size.x, b.size.z);
        if (current > 0.001f) go.transform.localScale *= width / current;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        count++; return go;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        var b = new Bounds(go.transform.position, Vector3.zero);
        if (renderers.Length > 0) { b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); }
        return b;
    }
    static void PlaceGround(string name, Vector3 point, float yaw, float width, Transform parent)
    {
        var go = Spawn(name, point, yaw, width, parent);
        go.transform.position += Vector3.up * (point.y - BoundsOf(go).min.y + 0.015f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); Freeze(go); Occupied.Add(point);
    }
    static void Freeze(GameObject go)
    {
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        { rb.useGravity = false; rb.isKinematic = true; PrefabUtility.RecordPrefabInstancePropertyModifications(rb); }
    }
    static void Scatter(List<Vector3> sites, string[] names, int amount, float minWidth, float maxWidth, Transform parent, bool swimming)
    {
        // Sample a few anchors, then offset each member to avoid a regular grid.
        var anchors = Enumerable.Range(0, 6).Select(_ => sites[random.Next(sites.Count)]).ToArray();
        for (int i = 0, attempts = 0; i < amount && attempts < amount * 100; attempts++)
        {
            Vector3 anchor = anchors[i % anchors.Length];
            Vector3 p = anchor + new Vector3((float)(random.NextDouble() - 0.5) * 7, 0, (float)(random.NextDouble() - 0.5) * 7);
            if (!Ground(p, out p, out var normal) || normal.y < 0.88f || Vector3.Distance(p, player.transform.position) < 4 || Vector3.Distance(p, player.transform.position) > 65) continue;
            if (parent.name.StartsWith("Beach") ? p.y < water + 0.03f || p.y > water + 1.3f : p.y >= water - 0.8f) continue;
            if (Occupied.Any(q => Vector3.Distance(q, p) < 0.65f)) continue;
            string name = names[i % names.Length];
            float width = Mathf.Lerp(minWidth, maxWidth, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360;
            if (swimming)
            {
                var go = Spawn(name, p, yaw, width, parent);
                var b = BoundsOf(go);
                float y = Mathf.Lerp(p.y + 0.5f + b.extents.y, water - 0.45f - b.extents.y, (float)random.NextDouble());
                go.transform.position += Vector3.up * (y - b.center.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); Freeze(go); Occupied.Add(p);
            }
            else PlaceGround(name, p, yaw, width, parent);
            i++;
        }
    }
}
