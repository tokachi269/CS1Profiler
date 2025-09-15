# ModTools Building Analysis Commands

## 建物総数確認
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var total = 0;
var active = 0;
var main = 0;
var sub = 0;
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var b = bm.m_buildings.m_buffer[i];
    if((b.m_flags & Building.Flags.Created) != 0) {
        total++;
        if((b.m_flags & Building.Flags.Active) != 0) active++;
        if(b.m_parentBuilding != 0) sub++;
        else main++;
    }
}
UnityEngine.Debug.Log("Total Buildings: " + total + ", Active: " + active + ", Main: " + main + ", Sub: " + sub);
```

## m_dirty状態確認
```csharp
var rm = ColossalFramework.Singleton<RenderManager>.instance;
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
int dirty = 0;
int valid = 0;
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var b = bm.m_buildings.m_buffer[i];
    if((b.m_flags & Building.Flags.Created) != 0) {
        uint idx;
        if(rm.RequireInstance((uint)i, 1U, out idx)) {
            valid++;
            if(rm.m_instances[idx].m_dirty) dirty++;
        }
    }
}
var percentage = valid > 0 ? dirty * 100.0 / valid : 0;
UnityEngine.Debug.Log("Valid Instances: " + valid + ", Dirty: " + dirty + ", Percentage: " + percentage.ToString("F2") + "%");
```

## 建物タイプ分布確認
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var services = new System.Collections.Generic.Dictionary<ItemClass.Service, int>();
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var b = bm.m_buildings.m_buffer[i];
    if((b.m_flags & Building.Flags.Created) != 0 && b.m_parentBuilding == 0 && b.Info != null) {
        var svc = b.Info.m_class.m_service;
        if(!services.ContainsKey(svc)) services[svc] = 0;
        services[svc]++;
    }
}
foreach(var kvp in services) {
    UnityEngine.Debug.Log(kvp.Key.ToString() + ": " + kvp.Value);
}
```

## 現在のRenderInstanceカウント
```csharp
var rm = ColossalFramework.Singleton<RenderManager>.instance;
int used = 0;
for(int i = 0; i < rm.m_instances.Length; i++) {
    if(rm.m_instances[i].m_initialized) used++;
}
UnityEngine.Debug.Log("RenderManager Instances Used: " + used + " / " + rm.m_instances.Length);
```

## フレーム内でのBuilding.RenderInstance呼び出し回数計測
```csharp
var counter = 0; var originalMethod = typeof(Building).GetMethod("RenderInstance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, null, new System.Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int) }, null); var patch = new HarmonyLib.HarmonyMethod(typeof(System.Action).GetMethod("Invoke")); var harmony = new HarmonyLib.Harmony("building.counter"); System.Action counterAction = () => { counter++; }; harmony.Patch(originalMethod, prefix: new HarmonyLib.HarmonyMethod(counterAction.Method)); UnityEngine.Debug.Log("Building.RenderInstance counter installed");
```

## 特定建物の詳細確認（IDを変更して実行）
```csharp
ushort buildingID = 1;
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var rm = ColossalFramework.Singleton<RenderManager>.instance;
if(buildingID < bm.m_buildings.m_buffer.Length) {
    var b = bm.m_buildings.m_buffer[buildingID];
    UnityEngine.Debug.Log("Building " + buildingID + ": Flags=" + b.m_flags + ", Parent=" + b.m_parentBuilding + ", Info=" + (b.Info != null ? b.Info.name : "null"));
    uint idx;
    if(rm.RequireInstance((uint)buildingID, 1U, out idx)) {
        UnityEngine.Debug.Log("Instance: dirty=" + rm.m_instances[idx].m_dirty + ", pos=" + rm.m_instances[idx].m_position);
    }
}
```

## BuildingManagerの内部統計
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
UnityEngine.Debug.Log("BuildingManager.m_buildingCount: " + bm.m_buildingCount);
UnityEngine.Debug.Log("BuildingManager buffer length: " + bm.m_buildings.m_buffer.Length);
UnityEngine.Debug.Log("BuildingManager.m_updatedBuildings: " + bm.m_updatedBuildings);
UnityEngine.Debug.Log("BuildingManager.m_buildingsRefreshed: " + bm.m_buildingsRefreshed);
```

## PublicTransport建物の詳細一覧
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
int count = 0;
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var b = bm.m_buildings.m_buffer[i];
    if((b.m_flags & Building.Flags.Created) != 0 && b.Info != null && b.Info.m_class.m_service == ItemClass.Service.PublicTransport) {
        count++;
        var pos = b.m_position;
        var name = b.Info.name;
        var subService = b.Info.m_class.m_subService;
        var flags = b.m_flags;
        var parent = b.m_parentBuilding;
        UnityEngine.Debug.Log("ID:" + i + " Name:" + name + " SubService:" + subService + " Pos:(" + pos.x.ToString("F0") + "," + pos.z.ToString("F0") + ") Flags:" + flags + " Parent:" + parent);
    }
}
UnityEngine.Debug.Log("Total PublicTransport buildings: " + count);
```

## PublicTransport建物のサブタイプ分布
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var subServices = new System.Collections.Generic.Dictionary<ItemClass.SubService, int>();
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var b = bm.m_buildings.m_buffer[i];
    if((b.m_flags & Building.Flags.Created) != 0 && b.Info != null && b.Info.m_class.m_service == ItemClass.Service.PublicTransport) {
        var sub = b.Info.m_class.m_subService;
        if(!subServices.ContainsKey(sub)) subServices[sub] = 0;
        subServices[sub]++;
    }
}
foreach(var kvp in subServices) {
    UnityEngine.Debug.Log(kvp.Key.ToString() + ": " + kvp.Value);
}
```

## 特定のPublicTransport建物のRenderInstance呼び出し回数測定
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var rm = ColossalFramework.Singleton<RenderManager>.instance;
var counters = new System.Collections.Generic.Dictionary<ushort, int>();
var harmony = new HarmonyLib.Harmony("pt.building.counter");
var originalMethod = typeof(Building).GetMethod("RenderInstance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, null, new System.Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int) }, null);
System.Action<object, RenderManager.CameraInfo, ushort, int> counterAction = (instance, cameraInfo, buildingID, layerMask) => {
    var b = bm.m_buildings.m_buffer[buildingID];
    if(b.Info != null && b.Info.m_class.m_service == ItemClass.Service.PublicTransport) {
        if(!counters.ContainsKey(buildingID)) counters[buildingID] = 0;
        counters[buildingID]++;
    }
};
var prefix = new HarmonyLib.HarmonyMethod(counterAction.Method);
harmony.Patch(originalMethod, prefix: prefix);
UnityEngine.Debug.Log("PublicTransport RenderInstance counter installed. Wait a few frames then check results.");
```

## PublicTransport建物のメイン・サブ建物関係
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var mainBuildings = new System.Collections.Generic.Dictionary<ushort, System.Collections.Generic.List<ushort>>();
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var b = bm.m_buildings.m_buffer[i];
    if((b.m_flags & Building.Flags.Created) != 0 && b.Info != null && b.Info.m_class.m_service == ItemClass.Service.PublicTransport) {
        if(b.m_parentBuilding == 0) {
            if(!mainBuildings.ContainsKey(i)) mainBuildings[i] = new System.Collections.Generic.List<ushort>();
        } else {
            var parent = b.m_parentBuilding;
            if(!mainBuildings.ContainsKey(parent)) mainBuildings[parent] = new System.Collections.Generic.List<ushort>();
            mainBuildings[parent].Add(i);
        }
    }
}
foreach(var kvp in mainBuildings) {
    var mainB = bm.m_buildings.m_buffer[kvp.Key];
    UnityEngine.Debug.Log("Main Building ID:" + kvp.Key + " Name:" + (mainB.Info != null ? mainB.Info.name : "null") + " Sub count:" + kvp.Value.Count);
    foreach(var subID in kvp.Value) {
        var subB = bm.m_buildings.m_buffer[subID];
        UnityEngine.Debug.Log("  Sub ID:" + subID + " Name:" + (subB.Info != null ? subB.Info.name : "null"));
    }
}
```

## 全建物のフラグ分布
```csharp
var bm = ColossalFramework.Singleton<BuildingManager>.instance;
var flags = new System.Collections.Generic.Dictionary<Building.Flags, int>();
for(ushort i = 0; i < bm.m_buildings.m_buffer.Length; i++) {
    var f = bm.m_buildings.m_buffer[i].m_flags;
    if(f != Building.Flags.None) {
        if(!flags.ContainsKey(f)) flags[f] = 0;
        flags[f]++;
    }
}
foreach(var kvp in flags) {
    UnityEngine.Debug.Log(kvp.Key.ToString() + ": " + kvp.Value);
}
```
