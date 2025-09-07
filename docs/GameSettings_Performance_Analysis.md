## GameSettings.InternalSaveAll パフォーマンス問題解析結果

### 問題の特定
`GameSettings.InternalSaveAll`が79ms/frameの実行時間を取る原因が判明しました。

### 根本原因
1. **巨大な設定ファイル**: `userGameState.cgs` (2.6MB) が最大の要因
2. **頻繁な保存**: バックグラウンドスレッドが1秒ごとに`SaveAll()`を実行
3. **ファイルI/O負荷**: 2.6MBのバイナリファイルの書き込みに時間がかかる

### 詳細分析

#### userGameState.cgsの保存内容
**1. アセット・MOD状態管理 (容量の大部分)**
```csharp
PackageManager.assetStateSettingsFile = Settings.userGameState;
PluginManager.assetStateSettingsFile = Settings.userGameState;
```
- 全ての有効/無効アセットとMODの状態情報
- 大量のMOD使用時に容量が膨大化

**2. 統計・カウンタ情報**
- `HotelEventsCount` - ホテルイベント数  
- `ScenarioSaveCount[各シナリオ名]` - シナリオ保存回数
- `ScenarioWinCount/LoseCount[各シナリオ名]` - クリア/失敗回数
- `nightPlayCount` - 夜間プレイ回数

**3. UI状態・その他設定**
- `loadingImageIndex` - ローディング画像
- 各種パネルの有効/無効状態
- アンロック状態

#### ファイルサイズ問題の根本原因
```
userGameState.cgs         2618.11 KB (2.6MB) ⚠️ MOD状態管理が異常肥大化
userGameStatem.cgs        2355.69 KB 
userGameState (2).cgs     2311.04 KB
gameSettings.cgs          1.1 KB (正常サイズ)
```

#### 実行頻度と影響
- MonitorSave()スレッドが1000ms間隔で実行
- 2.5分間で364回の`SaveAll()`呼び出し
- 平均79ms × 364回 = 28.8秒の累積実行時間
- 2.6MBファイルの毎秒書き込み = 毎秒2.6MBのディスクI/O

#### パフォーマンス影響
- CPU使用率: 6.9% (全体中3位の負荷)
- フレーム毎影響: 79.13ms/frame
- 毎秒8%のCPU時間をファイル保存に消費

### 推奨対策

#### 1. 緊急対策: ファイルサイズの調査
```csharp
// userGameState.cgsが異常に大きい原因を特定
// 不要なデータやメモリリークの可能性
if (File.Exists(userGameStatePath))
{
    var fileInfo = new FileInfo(userGameStatePath);
    if (fileInfo.Length > 100 * 1024) // 100KB以上は異常
    {
        Debug.LogWarning($"userGameState.cgs is unusually large: {fileInfo.Length / 1024}KB");
    }
}
```

#### 2. 保存頻度の最適化
```csharp
// MonitorSave()の間隔を延長
Thread.Sleep(5000); // 1秒 → 5秒に変更
```

#### 3. 差分保存の実装
```csharp
// isDirtyフラグの詳細化
if (settingsFile.isDirty && settingsFile.hasSignificantChanges)
{
    settingsFile.Save();
}
```

#### 4. ファイルサイズ制限の実装
```csharp
// 異常に大きいファイルの保存をスキップ
if (new FileInfo(pathName).Length > MAX_SETTINGS_FILE_SIZE)
{
    Debug.LogError("Settings file too large, skipping save");
    return;
}
```

### 結論
`GameSettings.InternalSaveAll`のパフォーマンス問題は、**異常に肥大化した`userGameState.cgs`ファイル（2.6MB）**を1秒ごとに保存することが原因です。通常のゲーム設定ファイルは数十KBであるべきで、2.6MBは明らかに異常です。

**主な問題:**
1. **ファイルサイズ異常**: 正常サイズの50-100倍
2. **毎秒ディスクI/O**: 2.6MB/秒の無駄な書き込み
3. **CPU使用率**: 毎秒8%のCPU時間をファイル保存に消費

**原因の特定:**
- **MOD/アセット状態管理**: `PackageManager`と`PluginManager`の状態がuserGameStateに保存
- **大量MOD使用**: 多数のMODを使用しているため状態情報が膨大
- **統計データ蓄積**: シナリオごとの統計情報が無制限に蓄積
- **不要な頻繁保存**: UI状態やカウンタが変更されるたびに保存

この問題を解決することで、FPS_Boosterよりも大きなパフォーマンス改善が期待できます。

### 次のステップ
1. `userGameState.cgs`の内容分析（不要データの特定）
2. 保存頻度最適化MODの開発
3. Cities: Skylines公式への改善提案
