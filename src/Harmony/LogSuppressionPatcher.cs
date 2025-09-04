using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// ログ抑制用のHarmonyパッチ管理
    /// PackageDeserializerの煩雑なログを抑制
    /// </summary>
    public static class LogSuppressionPatcher
    {
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    // ResolveLegacyMemberメソッドを完全に置き換え
                    var resolveLegacyMemberMethod = packageDeserializerType.GetMethod("ResolveLegacyMember", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (resolveLegacyMemberMethod != null)
                    {
                        harmony.Patch(resolveLegacyMemberMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(LogSuppressionHooks), "ResolveLegacyMember_Prefix"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer.ResolveLegacyMember replaced for log suppression");
                    }
                    
                    // ResolveLegacyTypeメソッドも置き換え
                    var resolveLegacyTypeMethod = packageDeserializerType.GetMethod("ResolveLegacyType", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (resolveLegacyTypeMethod != null)
                    {
                        harmony.Patch(resolveLegacyTypeMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(LogSuppressionHooks), "ResolveLegacyType_Replacement"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer.ResolveLegacyType replaced for log suppression");
                    }
                    
                    // HandleUnknownTypeメソッドも置き換え
                    var handleUnknownTypeMethod = packageDeserializerType.GetMethod("HandleUnknownType", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handleUnknownTypeMethod != null)
                    {
                        harmony.Patch(handleUnknownTypeMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(LogSuppressionHooks), "HandleUnknownType_Replacement"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer.HandleUnknownType replaced for log suppression");
                    }
                }
                
                UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer log suppression replacement patches completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageDeserializer log suppression replacement patch failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// PackageDeserializerのログ抑制を行うフッククラス
    /// </summary>
    public static class LogSuppressionHooks
    {
        // ログ抑制のオン/オフ設定（デフォルトはtrue=抑制する）
        public static bool SuppressPackageDeserializerLogs = true;

        // ResolveLegacyMemberメソッドの置き換え（ログ出力部分を除去）
        public static bool ResolveLegacyMember_Prefix(Type fieldType, Type classType, string member, ref string __result)
        {
            try
            {
                if (!SuppressPackageDeserializerLogs)
                {
                    // ログ抑制が無効な場合は元のメソッドを実行
                    return true;
                }

                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    var handlerField = packageDeserializerType.GetField("m_ResolveLegacyMemberHandler", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handlerField != null)
                    {
                        var handler = handlerField.GetValue(null);
                        if (handler != null)
                        {
                            // ハンドラーを呼び出して結果を取得（ログなし）
                            var delegateMethod = handler.GetType().GetMethod("Invoke");
                            var text = (string)delegateMethod.Invoke(handler, new object[] { classType, member });
                            __result = text;
                            return false; // 元のメソッドをスキップ（ログ抑制）
                        }
                    }
                }
                
                __result = member;
                return false; // 元のメソッドをスキップ
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ResolveLegacyMember_Prefix error: " + e.Message);
                return true; // エラー時は元のメソッドを実行
            }
        }

        // ResolveLegacyTypeメソッドの置き換え（ログ出力部分を除去）
        public static bool ResolveLegacyType_Replacement(string type, ref string __result)
        {
            try
            {
                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    var handlerField = packageDeserializerType.GetField("m_ResolveLegacyTypeHandler", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handlerField != null)
                    {
                        var handler = handlerField.GetValue(null);
                        if (handler != null)
                        {
                            var delegateType = typeof(Func<string, string>);
                            var invokeMethod = delegateType.GetMethod("Invoke");
                            var text = (string)invokeMethod.Invoke(handler, new object[] { type });
                            
                            // ログ抑制が無効な場合のみログを出力
                            if (!SuppressPackageDeserializerLogs)
                            {
                                // ログ出力処理（省略可能）
                                UnityEngine.Debug.LogWarning("[PackageDeserializer] Unknown type detected. Attempting to resolve from '" + type + "' to '" + text + "'");
                            }
                            
                            __result = text;
                            return false;
                        }
                    }
                }
                
                __result = type;
                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ResolveLegacyType_Replacement error: " + e.Message);
                return true;
            }
        }

        // HandleUnknownTypeメソッドの置き換え（ログ出力部分を除去）
        public static bool HandleUnknownType_Replacement(string type, ref int __result)
        {
            try
            {
                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    var handlerField = packageDeserializerType.GetField("m_UnknownTypeHandler", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handlerField != null)
                    {
                        var handler = handlerField.GetValue(null);
                        if (handler != null)
                        {
                            var delegateType = typeof(Func<string, int>);
                            var invokeMethod = delegateType.GetMethod("Invoke");
                            var num = (int)invokeMethod.Invoke(handler, new object[] { type });
                            
                            // ログ抑制が無効な場合のみログを出力
                            if (!SuppressPackageDeserializerLogs)
                            {
                                UnityEngine.Debug.LogWarning("[PackageDeserializer] Unexpected type '" + type + "' detected. No resolver handled this type. Skipping " + num + " bytes.");
                            }
                            
                            __result = num;
                            return false;
                        }
                    }
                }
                
                __result = -1;
                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] HandleUnknownType_Replacement error: " + e.Message);
                return true;
            }
        }
    }
}
