using HarmonyLib;
using System;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// パッチャーの統一インターフェース
    /// 型安全で拡張可能な設計
    /// </summary>
    public interface IPatchProvider
    {
        /// <summary>
        /// パッチが現在有効かどうか
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// パッチを有効化
        /// </summary>
        /// <param name="harmony">Harmonyインスタンス</param>
        void Enable(HarmonyLib.Harmony harmony);

        /// <summary>
        /// パッチを無効化
        /// </summary>
        /// <param name="harmony">Harmonyインスタンス</param>
        void Disable(HarmonyLib.Harmony harmony);

        /// <summary>
        /// パッチの種類名（デバッグ用）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// デフォルトで有効かどうか
        /// </summary>
        bool DefaultEnabled { get; }
    }
}
