# Building.cs m_dirtyフラグ調査結果

## 実装概要

### Building.RenderInstanceの実行フロー

**1. public RenderInstance (14-32行目)**
```csharp
public void RenderInstance(RenderManager.CameraInfo cameraInfo, ushort buildingID, int layerMask)
{
    // 基本チェック
    if ((flags & (Building.Flags.Created | Building.Flags.Deleted | Building.Flags.Hidden)) != Building.Flags.Created) return;
    if ((layerMask & 1 << info.m_prefabDataLayer) == 0) return;
    if (!cameraInfo.Intersect(position, radius)) return;
    
    // RenderManagerインスタンス取得
    if (instance.RequireInstance((uint)buildingID, 1U, out num))
    {
        this.RenderInstance(cameraInfo, buildingID, layerMask, info, ref instance.m_instances[num]);
    }
}
```

**2. private RenderInstance (35-49行目)**
```csharp
private void RenderInstance(RenderManager.CameraInfo cameraInfo, ushort buildingID, int layerMask, BuildingInfo info, ref RenderManager.Instance data)
{
    if (data.m_dirty)
    {
        data.m_dirty = false;
        info.m_buildingAI.RefreshInstance(cameraInfo, buildingID, ref this, layerMask, ref data);
    }
    info.m_buildingAI.RenderInstance(cameraInfo, buildingID, ref this, layerMask, ref data);
    // 通知処理
}
```

## m_dirtyフラグの動作パターン

### 検出されたm_dirtyの設定・使用箇所

**設定箇所（m_dirty = true）:**
- `InstanceManager.cs:98` - `nameData.m_dirty = true;`

**リセット箇所（m_dirty = false）:**
- `Building.cs:42` - `data.m_dirty = false;`
- `InstanceManager.cs:152` - 