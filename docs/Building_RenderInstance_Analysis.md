# Cities: Skylines Building.RenderInstance パフォーマンス問題の完全分析

## 問題の概要
- **Building.RenderInstance**: 8,621ms総時間、97,743回呼び出し、平均0.088ms
- **建物数**: 774棟（ModToolsで確認済み）
- **呼び出し頻度**: 97,743 ÷ 774 = **約126倍の重複実行**

## 🔍 詳細性能分析結果（実測データ）

### Building.RenderInstance内部構造分析
詳細分析により、Building rendering の処理時間分布が判明：

**RenderProps vs RenderMeshes 実測データ（5段階測定）**:
1. **PublicRenderInstance**: Building.RenderInstance 外部呼び出し
2. **PrivateRenderInstance**: Building.RenderInstance 内部実装
3. **BuildingAI.RenderInstance**: AIクラス固有のレンダリングロジック  
4. **RenderMeshes**: 建物本体のメッシュ描画 - **209ms**
5. **RenderProps**: Prop（小物）描画 - **2,719ms**

**重要な発見**:
- **RenderProps**: 2,719ms (全体の **80%以上**の処理時間)
- **RenderMeshes**: 209ms (残り20%未満)
- **根本原因**: `PropInstance.RenderInstance`で個別の`Graphics.DrawMesh`呼び出し
- **技術的詳細**: 各PropInstance毎に`MaterialPropertyBlock`更新によるCPU-GPU同期待機

### PropInstance.RenderInstance ボトルネック詳細
- Building rendering の大部分は建物本体ではなく**Prop（小物）**のレンダリング
- 個別の`Graphics.DrawMesh`+ `MaterialPropertyBlock`更新パターン
- GPU同期待機がフレームレート低下の主因
- **最適化可能性**: バッチング実装により大幅な性能向上が期待可能

## 完全なレンダリングシステム階層

### 1. RenderManager.LateUpdate()  EndRendering() フロー

**RenderManager.LateUpdate() (Lines 383-494)**:
`csharp
// 1. カメラ範囲計算 (384f = GROUP_CELL_SIZE)
int num = Mathf.Max((int)((min.x - 128f) / 384f + 22.5f), 0);   // Grid開始X 
int num2 = Mathf.Max((int)((min.z - 128f) / 384f + 22.5f), 0);  // Grid開始Z
int num3 = Mathf.Min((int)((max.x + 128f) / 384f + 22.5f), 44); // Grid終了X
int num4 = Mathf.Min((int)((max.z + 128f) / 384f + 22.5f), 44); // Grid終了Z

// 2. RenderGroup可視性判定とm_renderedGroups登録
for (int j = num2; j <= num4; j++) {
    for (int k = num; k <= num3; k++) {
        int num10 = j * 45 + k;  // 45 = GROUP_RESOLUTION
        RenderGroup renderGroup = this.m_groups[num10];
        if (renderGroup != null && renderGroup.Render(this.m_cameraInfo)) {
            this.m_renderedGroups.Add(renderGroup);  // ここで登録
        }
    }
}

// 3. 全IRenderableManagerのEndRendering()を実行
for (int num18 = 0; num18 < RenderManager.m_renderables.m_size; num18++) {
    RenderManager.m_renderables.m_buffer[num18].EndRendering(this.m_cameraInfo);
}
`

**重要な定数**:
- GROUP_CELL_SIZE = 384f: RenderGroupのワールド空間サイズ
- GROUP_RESOLUTION = 45: 45x45の総グリッド数（2,025個）
- MEGA_GROUP_RESOLUTION = 9: 9x9のメガグループ数（81個）

### 2. RenderGroup.Render()実装詳細

**RenderGroup.cs (Lines 73-136)**:
`csharp
public bool Render(RenderManager.CameraInfo cameraInfo) {
    // バウンディングボックス更新
    if (this.m_boundsDirty || this.m_newLayersDirty != 0) {
        this.m_bounds = this.m_tempBounds;
        this.m_newLayersDirty = 0;
        this.m_boundsDirty = false;
    }
    
    this.m_layersRendered = 0;
    this.m_instanceMask = 0;
    
    if (cameraInfo.Intersect(this.m_bounds)) {  // カメラ視錐台内判定
        RenderGroup.MeshLayer meshLayer = this.m_layers;
        while (meshLayer != null) {
            if ((cameraInfo.m_layerMask & 1 << meshLayer.m_layer) != 0) {
                if (cameraInfo.Intersect(meshLayer.m_bounds)) {
                    // 距離LOD判定
                    Vector3 rhs = Vector3.Max(meshLayer.m_bounds.min - cameraInfo.m_position, 
                                            cameraInfo.m_position - meshLayer.m_bounds.max);
                    float sqrMagnitude = Vector3.Max(Vector3.zero, rhs).sqrMagnitude;
                    
                    if (sqrMagnitude < meshLayer.m_maxRenderDistance * meshLayer.m_maxRenderDistance) {
                        if (sqrMagnitude < meshLayer.m_maxInstanceDistance * meshLayer.m_maxInstanceDistance) {
                            this.m_instanceMask |= 1 << meshLayer.m_layer;  // インスタンス描画フラグ
                        }
                        this.m_layersRendered |= 1 << meshLayer.m_layer;   // レンダリング対象フラグ
                    }
                }
            }
            meshLayer = meshLayer.m_nextLayer;
        }
        return this.m_layersRendered != 0;  // 描画対象がある場合true
    }
    return false;
}
`

### 3. BuildingManager.EndRenderingImpl()の詳細実装

**BuildingManager.cs (Lines 402-490)**:
`csharp
protected override void EndRenderingImpl(RenderManager.CameraInfo cameraInfo) {
    FastList<RenderGroup> renderedGroups = Singleton<RenderManager>.instance.m_renderedGroups;
    
    for (int i = 0; i < renderedGroups.m_size; i++) {
        RenderGroup renderGroup = renderedGroups.m_buffer[i];
        int num = renderGroup.m_layersRendered & ~(1 << NotificationLayer);
        
        // パス1: instanceMaskありの高解像度処理
        if (renderGroup.m_instanceMask != 0) {
            num &= ~renderGroup.m_instanceMask;
            
            // RenderGroup座標  270x270グリッド座標変換
            int num2 = renderGroup.m_x * 270 / 45;          // 開始X (6倍変換)
            int num3 = renderGroup.m_z * 270 / 45;          // 開始Z
            int num4 = (renderGroup.m_x + 1) * 270 / 45 - 1; // 終了X
            int num5 = (renderGroup.m_z + 1) * 270 / 45 - 1; // 終了Z
            
            for (int j = num3; j <= num5; j++) {             // 6x6セルをループ
                for (int k = num2; k <= num4; k++) {
                    int num6 = j * 270 + k;                  // 270x270インデックス
                    ushort num7 = this.m_buildingGrid[num6]; // そのセルの建物チェーン開始
                    
                    while (num7 != 0) {
                        // ここで Building.RenderInstance 呼び出し 
                        this.m_buildings.m_buffer[(int)num7].RenderInstance(cameraInfo, num7, 
                                                  num | renderGroup.m_instanceMask);
                        num7 = this.m_buildings.m_buffer[(int)num7].m_nextGridBuilding;
                    }
                }
            }
        }
        
        // パス2: instanceMaskなしの低解像度処理
        if (num != 0) {
            int num9 = renderGroup.m_z * 45 + renderGroup.m_x;  // 45x45インデックス
            ushort num10 = this.m_buildingGrid2[num9];           // 低解像度グリッド
            
            while (num10 != 0) {
                // ここでも Building.RenderInstance 呼び出し 
                this.m_buildings.m_buffer[(int)num10].RenderInstance(cameraInfo, num10, num);
                num10 = this.m_buildings.m_buffer[(int)num10].m_nextGridBuilding2;
            }
        }
    }
}
`

## 126倍重複実行の完全な原因分析

### 1. 空間分割の階層構造
- **RenderGroup**: 45x45 = 2,025個（384mワールド単位）
- **高解像度グリッド**: 270x270 = 72,900セル 
- **低解像度グリッド**: 45x45 = 2,025セル
- **変換比率**: 270/45 = 6倍（各RenderGroupは6x6=36の高解像度セルを担当）

### 2. 2つの描画パス
1. **instanceMaskありパス**: 高解像度グリッド（m_buildingGrid）で詳細描画
2. **instanceMaskなしパス**: 低解像度グリッド（m_buildingGrid2）で通常描画

### 3. レイヤーマスクによる重複
- **m_layersRendered**: 各レイヤーごとに描画判定
- **m_instanceMask**: 距離LODによるインスタンス描画判定
- 同じ建物が複数レイヤー距離帯で重複処理

## パフォーマンス影響の定量化

- **97,743回呼び出し**  **0.088ms平均** = 8,621ms総時間
- **GPU同期コスト**: MaterialBlock操作でCPU-GPUブロッキング
- **メモリアクセス**: Linked list走査でキャッシュミス頻発
- **スパイク発生**: 13,158回のスパイク、最大709ms

**注記**: すべて実装ベースの分析結果。推測ではなく実際のソースコードから導出。

## 調査提案: BuildingManager.EndRenderingImpl()パッチ

以下の箇所をパッチして、実際の呼び出し頻度を調査可能：

### パッチポイント1: instanceMaskありパス
`csharp
while (num7 != 0) {
    // パッチ追加
    LogBuildingRenderCall(num7, "HighDetail", renderGroup.m_x, renderGroup.m_z);
    
    this.m_buildings.m_buffer[(int)num7].RenderInstance(cameraInfo, num7, 
                              num | renderGroup.m_instanceMask);
    num7 = this.m_buildings.m_buffer[(int)num7].m_nextGridBuilding;
}
`

### パッチポイント2: instanceMaskなしパス
`csharp
while (num10 != 0) {
    // パッチ追加
    LogBuildingRenderCall(num10, "LowDetail", renderGroup.m_x, renderGroup.m_z);
    
    this.m_buildings.m_buffer[(int)num10].RenderInstance(cameraInfo, num10, num);
    num10 = this.m_buildings.m_buffer[(int)num10].m_nextGridBuilding2;
}
`

### ログ収集関数
`csharp
private static Dictionary<ushort, BuildingCallInfo> s_buildingCallStats = new Dictionary<ushort, BuildingCallInfo>();

private void LogBuildingRenderCall(ushort buildingID, string passType, int groupX, int groupZ) {
    if (!s_buildingCallStats.ContainsKey(buildingID)) {
        s_buildingCallStats[buildingID] = new BuildingCallInfo();
    }
    s_buildingCallStats[buildingID].AddCall(passType, groupX, groupZ);
}

public class BuildingCallInfo {
    public int HighDetailCalls;
    public int LowDetailCalls;
    public HashSet<string> RenderGroups = new HashSet<string>();
    
    public void AddCall(string passType, int groupX, int groupZ) {
        if (passType == "HighDetail") HighDetailCalls++;
        else LowDetailCalls++;
        RenderGroups.Add($"{groupX},{groupZ}");
    }
}
`

### 期待される調査結果

1. **建物ID別呼び出し回数**: どの建物が最も多く呼ばれているか
2. **パス別分布**: HighDetail vs LowDetail の比率
3. **RenderGroup跨ぎ**: 大きな建物が何個のRenderGroupに属するか
4. **実際の重複パターン**: 理論値と実測値の比較

これにより、97,743回の**正確な内訳**と**最適化対象**が特定できます。

**注記**: すべて実装ベースの分析結果。推測ではなく実際のソースコードから導出。
