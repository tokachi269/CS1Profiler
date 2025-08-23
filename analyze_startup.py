#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Cities: Skylines 1 起動解析データ分析スクリプト
CS1 Startup Performance Analyzer のCSV出力を分析し、ボトルネックを特定する
"""

import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
from datetime import datetime
import argparse
import os
import glob

def analyze_startup_csv(csv_file_path):
    """
    起動解析CSVファイルを分析してボトルネックを特定
    """
    print(f"分析開始: {csv_file_path}")
    
    try:
        # CSVファイル読み込み
        df = pd.read_csv(csv_file_path)
        print(f"データ行数: {len(df)}")
        print(f"分析期間: {df['DateTime'].iloc[0]} ～ {df['DateTime'].iloc[-1]}")
        
        # 基本統計
        print("\n=== 基本統計 ===")
        startup_events = df[df['Category'] == 'Startup']
        if len(startup_events) > 0:
            total_time = startup_events['Duration(ms)'].sum() / 1000
            print(f"総起動時間: {total_time:.2f} 秒")
            
        # ボトルネック分析
        analyze_bottlenecks(df)
        
        # メモリ使用量分析
        analyze_memory_usage(df)
        
        # MOD別分析
        analyze_mods(df)
        
        # グラフ生成
        generate_charts(df, csv_file_path)
        
    except Exception as e:
        print(f"エラー: {e}")

def analyze_bottlenecks(df):
    """
    起動ボトルネックの特定と分析
    """
    print("\n=== ボトルネック分析 ===")
    
    # 起動関連イベントの実行時間分析
    startup_events = df[df['Category'].isin(['Startup', 'System', 'StartupSummary'])]
    
    if len(startup_events) > 0:
        # 実行時間でソート
        slow_events = startup_events.nlargest(10, 'Duration(ms)')
        
        print("最も時間がかかった処理 (Top 10):")
        for idx, row in slow_events.iterrows():
            print(f"  {row['EventType']:30s} {row['Duration(ms)']:8.1f}ms - {row['Description']}")
        
        # 特定のボトルネックポイント分析
        bottleneck_events = [
            'BOOTSTRAPPER_BOOT_END',
            'PACKAGEMANAGER_ENSURE_END', 
            'HARMONY_INIT_SUCCESS',
            'TotalTime'
        ]
        
        print(f"\n重要なボトルネックポイント:")
        for event in bottleneck_events:
            event_data = df[df['EventType'] == event]
            if len(event_data) > 0:
                duration = event_data.iloc[0]['Duration(ms)']
                print(f"  {event:30s} {duration:8.1f}ms")

def analyze_memory_usage(df):
    """
    メモリ使用量の分析
    """
    print("\n=== メモリ使用量分析 ===")
    
    # メモリデータが存在する行のみ抽出
    memory_data = df[df['MemoryMB'] > 0]
    
    if len(memory_data) > 0:
        start_memory = memory_data.iloc[0]['MemoryMB']
        end_memory = memory_data.iloc[-1]['MemoryMB']
        max_memory = memory_data['MemoryMB'].max()
        
        print(f"開始時メモリ使用量: {start_memory} MB")
        print(f"終了時メモリ使用量: {end_memory} MB")
        print(f"最大メモリ使用量: {max_memory} MB")
        print(f"メモリ増加量: {end_memory - start_memory} MB")
        
        # メモリ急増イベントを特定
        memory_increases = []
        for i in range(1, len(memory_data)):
            prev_mem = memory_data.iloc[i-1]['MemoryMB'] 
            curr_mem = memory_data.iloc[i]['MemoryMB']
            if curr_mem - prev_mem > 50:  # 50MB以上の急増
                memory_increases.append({
                    'event': memory_data.iloc[i]['EventType'],
                    'increase': curr_mem - prev_mem,
                    'description': memory_data.iloc[i]['Description']
                })
        
        if memory_increases:
            print(f"\nメモリ急増イベント (50MB以上):")
            for event in memory_increases:
                print(f"  {event['event']:30s} +{event['increase']}MB - {event['description']}")

def analyze_mods(df):
    """
    MOD別の初期化時間分析
    """
    print("\n=== MOD別分析 ===")
    
    # LoadingExtension関連のイベントを抽出
    loading_events = df[df['EventType'].str.contains('LOADING_', na=False)]
    
    if len(loading_events) > 0:
        # OnCreated と OnLevelLoaded の時間を集計
        mod_times = {}
        
        for idx, row in loading_events.iterrows():
            description = row['Description']
            if '.OnCreated()' in description or '.OnLevelLoaded()' in description:
                # MOD名を抽出
                mod_name = description.split('.')[0]
                if 'completed in' in description:
                    duration = row['Duration(ms)']
                    if mod_name not in mod_times:
                        mod_times[mod_name] = 0
                    mod_times[mod_name] += duration
        
        if mod_times:
            # 初期化時間でソート
            sorted_mods = sorted(mod_times.items(), key=lambda x: x[1], reverse=True)
            
            print("MOD初期化時間 (Top 10):")
            for mod_name, total_time in sorted_mods[:10]:
                print(f"  {mod_name:30s} {total_time:8.1f}ms")

def generate_charts(df, csv_file_path):
    """
    分析結果のグラフを生成
    """
    print("\n=== グラフ生成 ===")
    
    try:
        # 日本語フォント設定
        plt.rcParams['font.family'] = ['DejaVu Sans', 'Yu Gothic', 'Meiryo', 'MS Gothic']
        
        fig, axes = plt.subplots(2, 2, figsize=(15, 12))
        fig.suptitle('Cities: Skylines 1 起動解析レポート', fontsize=16)
        
        # 1. 起動イベントタイムライン
        startup_events = df[df['Category'] == 'Startup'].copy()
        if len(startup_events) > 0:
            startup_events['DateTime'] = pd.to_datetime(startup_events['DateTime'])
            startup_events = startup_events.sort_values('DateTime')
            
            axes[0,0].plot(range(len(startup_events)), startup_events['Duration(ms)'])
            axes[0,0].set_title('起動イベント実行時間')
            axes[0,0].set_xlabel('イベント順序')
            axes[0,0].set_ylabel('実行時間 (ms)')
        
        # 2. メモリ使用量変化
        memory_data = df[df['MemoryMB'] > 0].copy()
        if len(memory_data) > 0:
            memory_data['DateTime'] = pd.to_datetime(memory_data['DateTime'])
            memory_data = memory_data.sort_values('DateTime')
            
            axes[0,1].plot(range(len(memory_data)), memory_data['MemoryMB'])
            axes[0,1].set_title('メモリ使用量変化')
            axes[0,1].set_xlabel('時間経過')
            axes[0,1].set_ylabel('メモリ使用量 (MB)')
        
        # 3. カテゴリ別実行時間
        category_times = df.groupby('Category')['Duration(ms)'].sum().sort_values(ascending=False)
        if len(category_times) > 0:
            category_times.plot(kind='bar', ax=axes[1,0])
            axes[1,0].set_title('カテゴリ別総実行時間')
            axes[1,0].set_ylabel('実行時間 (ms)')
            axes[1,0].tick_params(axis='x', rotation=45)
        
        # 4. 重要イベントの実行時間分布
        important_events = df[df['EventType'].str.contains('_END', na=False)]
        if len(important_events) > 0:
            top_events = important_events.nlargest(8, 'Duration(ms)')
            axes[1,1].barh(range(len(top_events)), top_events['Duration(ms)'])
            axes[1,1].set_yticks(range(len(top_events)))
            axes[1,1].set_yticklabels(top_events['EventType'], fontsize=8)
            axes[1,1].set_title('主要イベント実行時間')
            axes[1,1].set_xlabel('実行時間 (ms)')
        
        plt.tight_layout()
        
        # ファイル名からチャート保存先を決定
        chart_path = csv_file_path.replace('.csv', '_analysis_chart.png')
        plt.savefig(chart_path, dpi=300, bbox_inches='tight')
        print(f"分析グラフ保存: {chart_path}")
        
    except Exception as e:
        print(f"グラフ生成エラー: {e}")

def main():
    parser = argparse.ArgumentParser(description='Cities Skylines 1 起動解析データ分析')
    parser.add_argument('csv_file', nargs='?', help='分析するCSVファイルパス')
    parser.add_argument('--all', action='store_true', help='現在のフォルダ内の全CSVファイルを分析')
    
    args = parser.parse_args()
    
    if args.all:
        # 現在のフォルダ内のCS1Profiler CSVファイルを全て分析
        csv_files = glob.glob('CS1Profiler_*.csv')
        if not csv_files:
            print("CS1Profiler_*.csv ファイルが見つかりません")
            return
        
        print(f"{len(csv_files)} 個のCSVファイルを発見しました")
        for csv_file in csv_files:
            print(f"\n{'='*50}")
            analyze_startup_csv(csv_file)
            
    elif args.csv_file:
        if os.path.exists(args.csv_file):
            analyze_startup_csv(args.csv_file)
        else:
            print(f"ファイルが見つかりません: {args.csv_file}")
    else:
        # ファイル指定なしの場合、最新のCSVファイルを自動検索
        csv_files = glob.glob('CS1Profiler_*.csv')
        if csv_files:
            latest_csv = max(csv_files, key=os.path.getctime)
            print(f"最新のCSVファイルを自動選択: {latest_csv}")
            analyze_startup_csv(latest_csv)
        else:
            print("CSVファイルが見つかりません")
            print("使用方法:")
            print("  python analyze_startup.py <csvfile>")
            print("  python analyze_startup.py --all")

if __name__ == "__main__":
    main()
