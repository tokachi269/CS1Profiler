#!/usr/bin/env python3
"""
拡張可能なメソッド分析スクリプト
指定されたメソッドリストに基づいてプロファイリングデータを分析
"""

import pandas as pd
import sys
import os
import json
from datetime import datetime
from pathlib import Path

# プリセットメソッドリスト定義
METHOD_PRESETS = {
    "lateupdate": {
        "name": "LateUpdate関連メソッド",
        "description": "RenderManager.LateUpdate内で直接呼び出されるメソッド",
        "methods": [
            # 基本処理 (Direct LateUpdate calls)
            "InfoManager.UpdateInfoMode",
            "RenderManager.UpdateCameraInfo", 
            "RenderManager.UpdateColorMap",
            
            # ループ処理 (Renderables)
            "BeginRendering",
            "EndRendering",
            
            # グループ処理 (RenderGroups)
            "RenderGroup.Render",
            "MegaRenderGroup.Render",
            
            # Light System
            "LightSystem.EndRendering",
            "Clear",  # lightBuffer.Clear(), overlayBuffer.Clear()
            
            # RenderInstance系
            "Building.RenderInstance",
            "NetSegment.RenderInstance", 
            "NetNode.RenderInstance",
            "Vehicle.RenderInstance",
            "PropInstance.RenderInstance",
            "CitizenInstance.RenderInstance",
            "TreeInstance.RenderInstance",
            "VehicleParked.RenderInstance",
            
            # その他のRendering
            "TerrainPatch.Render",
            "MultiEffect.RenderEffect",
            "LightEffect.RenderEffect"
        ]
    },
    
    "building": {
        "name": "Building関連メソッド",
        "description": "Building.RenderInstanceの実装に基づく正確なメソッドリスト",
        "methods": [
            # Building.RenderInstance本体 (Building.cs 14-61行)
            "Building.RenderInstance",
            
            # public RenderInstance内の呼び出し (14-32行)
            "RenderManager.RequireInstance",
            
            # private RenderInstance内の呼び出し (35-61行)
            "BuildingAI.RefreshInstance",
            "BuildingAI.RenderInstance", 
            "Notification.RenderInstance",
            
            # RefreshInstance内で呼ばれるメソッド (重い処理)
            "TerrainManager.SampleDetailHeight",
            "TerrainManager.GetHeightMapping",
            "BuildingInfo.RefreshLevelUp",
            
            # BuildingAI具象クラス別のRefreshInstance/RenderInstance
            "ResidentialBuildingAI.RefreshInstance",
            "ResidentialBuildingAI.RenderInstance",
            "CommercialBuildingAI.RefreshInstance",
            "CommercialBuildingAI.RenderInstance",
            "IndustrialBuildingAI.RefreshInstance", 
            "IndustrialBuildingAI.RenderInstance",
            "OfficeBuildingAI.RefreshInstance",
            "OfficeBuildingAI.RenderInstance",
            "ServiceBuildingAI.RefreshInstance",
            "ServiceBuildingAI.RenderInstance",
            "MonumentBuildingAI.RefreshInstance",
            "MonumentBuildingAI.RenderInstance",
            "PublicTransportStationAI.RefreshInstance",
            "PublicTransportStationAI.RenderInstance",
            "TransportStationAI.RefreshInstance",
            "TransportStationAI.RenderInstance",
            
            # 描画関連の低レベルメソッド
            "Graphics.DrawMesh",
            "MaterialPropertyBlock.SetMatrix",
            "MaterialPropertyBlock.SetColor",
            "MaterialPropertyBlock.SetVector",
            "MaterialPropertyBlock.SetTexture"
        ]
    },
    
    "ui": {
        "name": "UI関連メソッド",
        "description": "ユーザーインターフェース描画に関連するメソッド",
        "methods": [
            "UIView.Render",
            "UIView.LateUpdate",
            "UIView.FpsBoosterLateUpdate",
            "DynamicFontRenderer.Render",
            "DynamicFontRenderer.MeasureString",
            "UIRenderData.Merge",
            "UIRenderData.Release",
            "UIRenderData.Clear",
            "UIComponent.Render",
            "UIPanel.Render"
        ]
    },
    
    "terrain": {
        "name": "地形関連メソッド",
        "description": "地形描画・処理に関連するメソッド",
        "methods": [
            "TerrainManager",
            "TerrainPatch.Render",
            "TerrainManager.SampleDetailHeight",
            "TerrainManager.GetHeightMapping",
            "WaterManager",
            "WaterSimulation"
        ]
    },
    
    "effects": {
        "name": "エフェクト関連メソッド",
        "description": "視覚エフェクト描画に関連するメソッド",
        "methods": [
            "MultiEffect.RenderEffect",
            "LightEffect.RenderEffect",
            "ParticleEffect",
            "SoundEffect",
            "WindEffect",
            "WeatherEffect"
        ]
    }
}

def load_custom_methods(file_path):
    """
    カスタムメソッドリストをJSONファイルから読み込み
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:
        print(f"カスタムメソッドファイル読み込みエラー: {e}")
        return None

def analyze_methods(csv_file, preset_name=None, custom_file=None, method_list=None):
    """
    指定されたメソッドリストでプロファイリングデータを分析
    """
    print(f"=== メソッド分析開始 ===")
    print(f"CSVファイル: {csv_file}")
    
    # メソッドリストの決定
    methods_to_analyze = []
    analysis_name = ""
    
    if custom_file:
        custom_data = load_custom_methods(custom_file)
        if custom_data:
            methods_to_analyze = custom_data.get('methods', [])
            analysis_name = custom_data.get('name', 'カスタム分析')
            print(f"カスタムメソッドファイル使用: {custom_file}")
        else:
            print("カスタムファイル読み込み失敗、デフォルトに切り替え")
            preset_name = "lateupdate"
    
    if preset_name and not methods_to_analyze:
        if preset_name in METHOD_PRESETS:
            preset = METHOD_PRESETS[preset_name]
            methods_to_analyze = preset['methods']
            analysis_name = preset['name']
            print(f"プリセット使用: {preset_name} - {preset['description']}")
        else:
            print(f"不明なプリセット: {preset_name}")
            print(f"利用可能なプリセット: {', '.join(METHOD_PRESETS.keys())}")
            return
    
    if method_list and not methods_to_analyze:
        methods_to_analyze = method_list
        analysis_name = "コマンドライン指定"
        print(f"コマンドライン指定メソッド: {len(method_list)}個")
    
    if not methods_to_analyze:
        methods_to_analyze = METHOD_PRESETS['lateupdate']['methods']
        analysis_name = "LateUpdate関連メソッド (デフォルト)"
        print("デフォルトのLateUpdateメソッドリストを使用")
    
    print(f"分析対象: {analysis_name}")
    print(f"メソッド数: {len(methods_to_analyze)}")
    
    try:
        # CSVファイルを読み込み
        print("\nCSVファイル読み込み中...")
        df = pd.read_csv(csv_file)
        print(f"総レコード数: {len(df)}")
        
        # メソッド分析実行
        found_methods = []
        total_time = 0
        
        # CSVファイルの列名を確認・対応
        column_mapping = {
            'Method': 'MethodName',
            'TotalTime_ms': 'TotalImpactMs', 
            'CallCount': 'TotalCalls',
            'AvgTime_ms': 'AvgDurationMs',
            'MaxTime_ms': 'MaxDurationMs'
        }
        
        # 列名を統一
        df_renamed = df.rename(columns=column_mapping)
        
        # 不足する列を追加（デフォルト値）
        if 'FramesActive' not in df_renamed.columns:
            df_renamed['FramesActive'] = 0
        if 'ImpactPercentage' not in df_renamed.columns:
            total_all_time = df_renamed['TotalImpactMs'].sum()
            df_renamed['ImpactPercentage'] = (df_renamed['TotalImpactMs'] / total_all_time * 100) if total_all_time > 0 else 0
        
        for method_pattern in methods_to_analyze:
            # パターンマッチング（部分一致）
            matching_methods = df_renamed[df_renamed['MethodName'].str.contains(method_pattern, case=False, na=False)]
            
            if not matching_methods.empty:
                method_total_time = matching_methods['TotalImpactMs'].sum()
                total_time += method_total_time
                
                for _, method in matching_methods.iterrows():
                    found_methods.append({
                        'Pattern': method_pattern,
                        'MethodName': method['MethodName'],
                        'Category': method.get('Category', 'Pattern_Match') if method_pattern in method['MethodName'] else 'Direct_Match',
                        'TotalTime': method['TotalImpactMs'],
                        'Calls': method['TotalCalls'],
                        'AvgTime': method['AvgDurationMs'],
                        'MaxTime': method['MaxDurationMs'],
                        'SpikeCount': method['SpikeCount'],
                        'FramesActive': method['FramesActive'],
                        'ImpactPercentage': method['ImpactPercentage']
                    })
        
        # 結果をソート
        found_methods.sort(key=lambda x: x['TotalTime'], reverse=True)
        
        print(f"\n=== {analysis_name} 分析結果 ===")
        print(f"発見されたメソッド数: {len(found_methods)}")
        print(f"総実行時間: {total_time:.2f}ms")
        
        if found_methods:
            total_percentage = sum(method['ImpactPercentage'] for method in found_methods)
            print(f"全体に占める割合: {total_percentage:.1f}%")
        
        print(f"\n=== 高負荷メソッド Top 20 ===")
        for i, method in enumerate(found_methods[:20], 1):
            print(f"{i:2d}. {method['MethodName']}")
            print(f"    総時間: {method['TotalTime']:.2f}ms, 呼び出し: {method['Calls']:,}回")
            print(f"    平均: {method['AvgTime']:.4f}ms, 最大: {method['MaxTime']:.2f}ms, スパイク: {method['SpikeCount']:,}回")
            print(f"    パターン: {method['Pattern']}, 影響度: {method['ImpactPercentage']:.2f}%")
            print()
        
        # カテゴリ別集計
        if found_methods:
            print("=== カテゴリ別集計 ===")
            category_stats = {}
            for method in found_methods:
                cat = method['Category']
                if cat not in category_stats:
                    category_stats[cat] = {'total_time': 0, 'total_calls': 0, 'method_count': 0}
                category_stats[cat]['total_time'] += method['TotalTime']
                category_stats[cat]['total_calls'] += method['Calls']
                category_stats[cat]['method_count'] += 1
            
            for category, stats in category_stats.items():
                print(f"{category}: {stats['total_time']:.2f}ms ({stats['total_calls']:,}回, {stats['method_count']}メソッド)")
        
        # 詳細CSVファイルを出力
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = f"method_analysis_{preset_name or 'custom'}_{timestamp}.csv"
        
        if found_methods:
            methods_df = pd.DataFrame(found_methods)
            methods_df.to_csv(output_file, index=False, encoding='utf-8-sig')
            print(f"\n詳細結果を {output_file} に出力しました。")
        
    except Exception as e:
        print(f"エラーが発生しました: {e}")
        import traceback
        traceback.print_exc()

def list_presets():
    """
    利用可能なプリセット一覧を表示
    """
    print("=== 利用可能なプリセット ===")
    for key, preset in METHOD_PRESETS.items():
        print(f"{key}: {preset['name']}")
        print(f"  説明: {preset['description']}")
        print(f"  メソッド数: {len(preset['methods'])}")
        print()

def create_custom_template(output_file):
    """
    カスタムメソッドリストのテンプレートを作成
    """
    template = {
        "name": "カスタム分析",
        "description": "カスタムメソッドリストによる分析",
        "methods": [
            "Example.Method1",
            "Example.Method2",
            "Pattern.*",
            "AnotherPattern"
        ]
    }
    
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(template, f, ensure_ascii=False, indent=2)
    
    print(f"カスタムメソッドテンプレートを {output_file} に作成しました。")
    print("このファイルを編集して --custom オプションで使用してください。")

def main():
    if len(sys.argv) < 2:
        print("使用方法:")
        print("  python flexible_method_analyzer.py <method_statistics.csv> [options]")
        print("")
        print("オプション:")
        print("  --preset <name>     : プリセットを使用 (lateupdate, building, ui, terrain, effects)")
        print("  --custom <file>     : カスタムJSONファイルを使用")
        print("  --methods <m1,m2>   : メソッド名をカンマ区切りで直接指定")
        print("  --list-presets      : 利用可能なプリセット一覧を表示")
        print("  --create-template <file> : カスタムメソッドテンプレートを作成")
        print("")
        print("例:")
        print("  python flexible_method_analyzer.py data.csv --preset building")
        print("  python flexible_method_analyzer.py data.csv --custom my_methods.json")
        print("  python flexible_method_analyzer.py data.csv --methods 'Building,UI,Terrain'")
        sys.exit(1)
    
    csv_file = sys.argv[1]
    preset_name = None
    custom_file = None
    method_list = None
    
    # コマンドライン引数の解析
    i = 2
    while i < len(sys.argv):
        if sys.argv[i] == '--preset' and i + 1 < len(sys.argv):
            preset_name = sys.argv[i + 1]
            i += 2
        elif sys.argv[i] == '--custom' and i + 1 < len(sys.argv):
            custom_file = sys.argv[i + 1]
            i += 2
        elif sys.argv[i] == '--methods' and i + 1 < len(sys.argv):
            method_list = [m.strip() for m in sys.argv[i + 1].split(',')]
            i += 2
        elif sys.argv[i] == '--list-presets':
            list_presets()
            sys.exit(0)
        elif sys.argv[i] == '--create-template' and i + 1 < len(sys.argv):
            create_custom_template(sys.argv[i + 1])
            sys.exit(0)
        else:
            print(f"不明なオプション: {sys.argv[i]}")
            sys.exit(1)
    
    if not os.path.exists(csv_file):
        print(f"ファイルが見つかりません: {csv_file}")
        sys.exit(1)
    
    analyze_methods(csv_file, preset_name, custom_file, method_list)

if __name__ == "__main__":
    main()
