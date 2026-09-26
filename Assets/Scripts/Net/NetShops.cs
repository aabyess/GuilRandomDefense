using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬의 레인 상점(ILaneShop)을 번호로 가리킨다. 상점은 씬 고정물이라 호스트·클라가 같은 씬이면
/// **계층 경로 순**으로 정렬한 목록이 같다 — 그 순번을 네트워크로 보낸다.
/// 게임 씬이 새로 로드되면 목록을 다시 만든다.
/// </summary>
public static class NetShops
{
    static readonly List<ILaneShop> shops = new List<ILaneShop>();
    static int builtForScene = -1;

    static void EnsureBuilt()
    {
        int handle = SceneManager.GetActiveScene().handle;
        if (builtForScene == handle && shops.Count > 0) return;
        builtForScene = handle;
        shops.Clear();

        var found = new List<(string path, ILaneShop shop)>();
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour is ILaneShop shop) found.Add((PathOf(behaviour), shop));
        found.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
        foreach (var entry in found) shops.Add(entry.shop);

        Debug.Log($"[MP] 상점 목록: {shops.Count}개");
    }

    static string PathOf(MonoBehaviour behaviour)
    {
        Transform t = behaviour.transform;
        string path = t.name + "#" + behaviour.GetType().Name + "#" + t.GetSiblingIndex();
        for (t = t.parent; t != null; t = t.parent) path = t.name + "#" + t.GetSiblingIndex() + "/" + path;
        return path;
    }

    public static int IdOf(ILaneShop shop)
    {
        EnsureBuilt();
        return shops.IndexOf(shop);
    }

    public static ILaneShop Get(int id)
    {
        EnsureBuilt();
        return id >= 0 && id < shops.Count ? shops[id] : null;
    }
}
