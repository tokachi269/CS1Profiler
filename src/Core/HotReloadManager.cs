using System;
using System.Collections.Generic;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// システムリセット・再初期化マネージャー
    /// 全インスタンスを破棄→再作成する汎用システム
    /// </summary>
    public static class SystemManager
    {
        // 管理対象のGameObject一覧
        private static readonly List<GameObject> managedObjects = new List<GameObject>();
        
        // 再作成用のファクトリ関数
        private static Action recreateSystemsCallback;

        /// <summary>
        /// GameObject登録（破棄対象として）
        /// </summary>
        public static void RegisterObject(GameObject obj)
        {
            if (obj != null && !managedObjects.Contains(obj))
            {
                managedObjects.Add(obj);
                Debug.Log($"[SystemManager] Registered: {obj.name}");
            }
        }

        /// <summary>
        /// 再作成用のコールバック設定
        /// </summary>
        public static void SetRecreateCallback(Action callback)
        {
            recreateSystemsCallback = callback;
        }

        /// <summary>
        /// システムリセット実行：全破棄→再作成
        /// </summary>
        public static void ResetAllSystems()
        {
            try
            {
                Debug.Log("[SystemManager] === STARTING SYSTEM RESET ===");

                // 1. 全GameObjectを破棄
                DestroyAllManagedObjects();

                // 2. Harmonyパッチ解除
                UnpatchHarmony();

                // 3. 少し待つ
                System.Threading.Thread.Sleep(100);

                // 4. 全システム再作成
                RecreateAllSystems();

                Debug.Log("[SystemManager] === SYSTEM RESET COMPLETED ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SystemManager] Reset failed: {e}");
            }
        }

        private static void DestroyAllManagedObjects()
        {
            Debug.Log($"[SystemManager] Destroying {managedObjects.Count} objects...");
            
            foreach (var obj in managedObjects)
            {
                if (obj != null)
                {
                    Debug.Log($"[SystemManager] Destroying: {obj.name}");
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
            
            managedObjects.Clear();
        }

        private static void UnpatchHarmony()
        {
            try
            {
                Debug.Log("[SystemManager] Unpatching Harmony...");
                Patcher.UnpatchAll();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SystemManager] Harmony unpatch warning: {e.Message}");
            }
        }

        private static void RecreateAllSystems()
        {
            Debug.Log("[SystemManager] Recreating all systems...");
            
            if (recreateSystemsCallback != null)
            {
                recreateSystemsCallback.Invoke();
            }
            else
            {
                Debug.LogWarning("[SystemManager] No recreate callback set!");
            }
        }
    }
}
