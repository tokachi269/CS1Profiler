#!/usr/bin/env python3
"""
CS1Profiler CSV Analysis Tool
Cities: Skylines 1 プロファイラーのCSVデータを解析・可視化するツール
"""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import seaborn as sns
import argparse
import os
from datetime import datetime
import warnings
warnings.filterwarnings('ignore')

# 日本語フォント設定（Windows環境対応）
plt.rcParams['font.family'] = ['DejaVu Sans', 'Yu Gothic', 'Hiragino Sans', 'Noto Sans CJK JP']
plt.rcParams['figure.figsize'] = (12, 8)

class CS1ProfilerAnalyzer:
    def __init__(self, csv_file):
        """CSVファイルを読み込んで初期化"""
        self.csv_file = csv_file
        self.df = None
        self.load_data()
    
    def load_data(self):
        """CSVデータを読み込み"""
        try:
            self.df = pd.read_csv(self.csv_file)
            self.df['DateTime'] = pd.to_datetime(self.df['DateTime'])
            self.df['TotalDurationPerFrame'] = self.df['Duration(ms)'] * self.df['Count']
            print(f"✅ データ読み込み完了: {len(self.df)} レコード")
            print(f"📅 期間: {self.df['DateTime'].min()} ～ {self.df['DateTime'].max()}")
            print(f"🎮 フレーム範囲: {self.df['FrameCount'].min()} ～ {self.df['FrameCount'].max()}")
        except Exception as e:
            print(f"❌ CSVファイル読み込みエラー: {e}")
            raise

    def method_statistics(self, spike_multiplier=2.0):
        """メソッド別統計情報を生成"""
        print("\n📊 メソッド別統計情報を生成中...")
        
        method_stats = []
        
        for method_name, method_data in self.df.groupby('Description'):
            durations = method_data['Duration(ms)']
            frame_totals = method_data.groupby('FrameCount')['TotalDurationPerFrame'].sum()
            frame_calls = method_data.groupby('FrameCount')['Count'].sum()
            
            avg_duration = durations.mean()
            spike_threshold = avg_duration * spike_multiplier
            spike_records = method_data[method_data['Duration(ms)'] > spike_threshold]
            
            stats = {
                'MethodName': method_name,
                'Category': method_data['Category'].iloc[0],
                'TotalCalls': method_data['Count'].sum(),
                'AvgDurationMs': avg_duration,
                'MaxDurationMs': durations.max(),
                'MinDurationMs': durations.min(),
                'StdDevMs': durations.std(),
                'FramesActive': len(frame_totals),
                'AvgTotalPerFrameMs': frame_totals.mean(),
                'MaxTotalPerFrameMs': frame_totals.max(),
                'SpikeCount': len(spike_records),
                'SpikeThreshold': spike_threshold,
                'AvgCallsPerFrame': frame_calls.mean(),
                'MaxCallsPerFrame': frame_calls.max(),
                'MinCallsPerFrame': frame_calls.min(),
                'AvgMemoryMB': method_data['MemoryMB'].mean(),
                'MaxMemoryMB': method_data['MemoryMB'].max(),
                # パフォーマンス指標
                'TotalImpactMs': frame_totals.sum(),
                'ImpactPercentage': 0,  # 後で計算
                'PerformanceScore': avg_duration * method_data['Count'].sum()  # 影響度スコア
            }
            method_stats.append(stats)
        
        # DataFrameに変換
        stats_df = pd.DataFrame(method_stats)
        
        # 影響度パーセンテージを計算
        total_impact = stats_df['TotalImpactMs'].sum()
        stats_df['ImpactPercentage'] = (stats_df['TotalImpactMs'] / total_impact * 100)
        
        # パフォーマンススコア順でソート
        stats_df = stats_df.sort_values('PerformanceScore', ascending=False)
        
        return stats_df

    def frame_statistics(self):
        """フレーム別統計情報を生成"""
        print("\n📈 フレーム別統計情報を生成中...")
        
        frame_stats = []
        
        for frame_num, frame_data in self.df.groupby('FrameCount'):
            total_duration = frame_data['TotalDurationPerFrame'].sum()
            top_methods = frame_data.nlargest(5, 'TotalDurationPerFrame')[['Description', 'TotalDurationPerFrame']]
            
            stats = {
                'FrameNumber': frame_num,
                'FrameTime': frame_data['DateTime'].iloc[0],
                'TotalFrameMs': total_duration,
                'TotalCalls': frame_data['Count'].sum(),
                'UniqueMethodCount': len(frame_data),
                'TotalMemoryMB': frame_data['MemoryMB'].sum(),
                'TopMethod': top_methods.iloc[0]['Description'] if len(top_methods) > 0 else '',
                'TopMethodMs': top_methods.iloc[0]['TotalDurationPerFrame'] if len(top_methods) > 0 else 0
            }
            frame_stats.append(stats)
        
        return pd.DataFrame(frame_stats)

    def category_statistics(self):
        """カテゴリ別統計情報を生成"""
        print("\n🏷️ カテゴリ別統計情報を生成中...")
        
        category_stats = []
        
        for category, category_data in self.df.groupby('Category'):
            durations = category_data['Duration(ms)']
            frame_totals = category_data.groupby('FrameCount')['TotalDurationPerFrame'].sum()
            
            stats = {
                'Category': category,
                'MethodCount': len(category_data['Description'].unique()),
                'TotalCalls': category_data['Count'].sum(),
                'AvgDurationMs': durations.mean(),
                'MaxDurationMs': durations.max(),
                'StdDevMs': durations.std(),
                'TotalImpactMs': frame_totals.sum(),
                'AvgImpactPerFrameMs': frame_totals.mean(),
                'AvgMemoryMB': category_data['MemoryMB'].mean()
            }
            category_stats.append(stats)
        
        stats_df = pd.DataFrame(category_stats)
        return stats_df.sort_values('TotalImpactMs', ascending=False)

    def detect_performance_issues(self, method_stats):
        """パフォーマンス問題を検出"""
        print("\n🚨 パフォーマンス問題を検出中...")
        
        issues = []
        
        # 高負荷メソッドの検出
        high_impact = method_stats[method_stats['ImpactPercentage'] > 5.0]
        for _, method in high_impact.iterrows():
            issues.append({
                'Type': '高負荷メソッド',
                'Method': method['MethodName'],
                'Issue': f"全体の {method['ImpactPercentage']:.1f}% を占める高負荷",
                'Value': f"{method['AvgTotalPerFrameMs']:.2f}ms/frame",
                'Severity': 'HIGH' if method['ImpactPercentage'] > 10 else 'MEDIUM'
            })
        
        # スパイク多発メソッドの検出
        spike_methods = method_stats[method_stats['SpikeCount'] > 10]
        for _, method in spike_methods.iterrows():
            issues.append({
                'Type': 'スパイク多発',
                'Method': method['MethodName'],
                'Issue': f"{method['SpikeCount']} 回のスパイク発生",
                'Value': f"最大 {method['MaxDurationMs']:.2f}ms",
                'Severity': 'HIGH' if method['SpikeCount'] > 50 else 'MEDIUM'
            })
        
        # 呼び出し回数異常の検出
        call_variance = method_stats[
            (method_stats['MaxCallsPerFrame'] / method_stats['AvgCallsPerFrame'] > 3) &
            (method_stats['AvgCallsPerFrame'] > 1)
        ]
        for _, method in call_variance.iterrows():
            issues.append({
                'Type': '呼び出し回数変動',
                'Method': method['MethodName'],
                'Issue': f"最大 {method['MaxCallsPerFrame']:.0f} 回/frame (平均 {method['AvgCallsPerFrame']:.1f})",
                'Value': f"変動率 {method['MaxCallsPerFrame']/method['AvgCallsPerFrame']:.1f}x",
                'Severity': 'MEDIUM'
            })
        
        return pd.DataFrame(issues)

    def generate_visualizations(self, method_stats, frame_stats, output_dir='analysis_output'):
        """可視化グラフを生成"""
        print(f"\n📊 可視化グラフを生成中... ({output_dir}/)")
        
        os.makedirs(output_dir, exist_ok=True)
        
        # 1. トップ15メソッドの影響度
        plt.figure(figsize=(14, 8))
        top15 = method_stats.head(15)
        bars = plt.barh(range(len(top15)), top15['AvgTotalPerFrameMs'])
        plt.yticks(range(len(top15)), [name[:40] + '...' if len(name) > 40 else name for name in top15['MethodName']])
        plt.xlabel('平均影響度 (ms/frame)')
        plt.title('CS1Profiler: トップ15 高負荷メソッド')
        plt.gca().invert_yaxis()
        
        # バーに数値を表示
        for i, bar in enumerate(bars):
            width = bar.get_width()
            plt.text(width + 0.01, bar.get_y() + bar.get_height()/2, 
                    f'{width:.2f}ms', ha='left', va='center')
        
        plt.tight_layout()
        plt.savefig(f'{output_dir}/top15_methods.png', dpi=300, bbox_inches='tight')
        plt.close()
        
        # 2. カテゴリ別影響度（円グラフ）
        category_stats = self.category_statistics()
        plt.figure(figsize=(10, 8))
        plt.pie(category_stats['TotalImpactMs'], labels=category_stats['Category'], autopct='%1.1f%%')
        plt.title('カテゴリ別パフォーマンス影響度')
        plt.savefig(f'{output_dir}/category_impact.png', dpi=300, bbox_inches='tight')
        plt.close()
        
        # 3. フレーム別負荷推移
        plt.figure(figsize=(15, 6))
        plt.plot(frame_stats['FrameNumber'], frame_stats['TotalFrameMs'], alpha=0.7)
        plt.xlabel('フレーム番号')
        plt.ylabel('総処理時間 (ms)')
        plt.title('フレーム別処理時間推移')
        plt.grid(True, alpha=0.3)
        
        # スパイクを強調表示
        spike_threshold = frame_stats['TotalFrameMs'].mean() + frame_stats['TotalFrameMs'].std() * 2
        spikes = frame_stats[frame_stats['TotalFrameMs'] > spike_threshold]
        if len(spikes) > 0:
            plt.scatter(spikes['FrameNumber'], spikes['TotalFrameMs'], 
                       color='red', s=50, alpha=0.8, label=f'スパイク({len(spikes)}回)')
            plt.legend()
        
        plt.tight_layout()
        plt.savefig(f'{output_dir}/frame_timeline.png', dpi=300, bbox_inches='tight')
        plt.close()
        
        # 4. スパイク分析（ヒストグラム）
        spike_methods = method_stats[method_stats['SpikeCount'] > 0].head(10)
        if len(spike_methods) > 0:
            plt.figure(figsize=(12, 6))
            plt.bar(range(len(spike_methods)), spike_methods['SpikeCount'])
            plt.xticks(range(len(spike_methods)), 
                      [name[:20] + '...' if len(name) > 20 else name for name in spike_methods['MethodName']], 
                      rotation=45, ha='right')
            plt.ylabel('スパイク回数')
            plt.title('メソッド別スパイク発生回数 (Top10)')
            plt.tight_layout()
            plt.savefig(f'{output_dir}/spike_analysis.png', dpi=300, bbox_inches='tight')
            plt.close()

    def export_results(self, method_stats, frame_stats, issues, output_dir='analysis_output'):
        """解析結果をエクスポート"""
        print(f"\n💾 解析結果をエクスポート中... ({output_dir}/)")
        
        os.makedirs(output_dir, exist_ok=True)
        
        # 統計結果のエクスポート
        method_stats.to_csv(f'{output_dir}/method_statistics.csv', index=False, encoding='utf-8-sig')
        frame_stats.to_csv(f'{output_dir}/frame_statistics.csv', index=False, encoding='utf-8-sig')
        issues.to_csv(f'{output_dir}/performance_issues.csv', index=False, encoding='utf-8-sig')
        
        # サマリーレポートの生成
        with open(f'{output_dir}/analysis_report.txt', 'w', encoding='utf-8') as f:
            f.write("CS1Profiler 解析レポート\n")
            f.write("=" * 50 + "\n")
            f.write(f"解析日時: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
            f.write(f"データファイル: {self.csv_file}\n")
            f.write(f"総レコード数: {len(self.df)}\n")
            f.write(f"解析フレーム数: {self.df['FrameCount'].nunique()}\n")
            f.write(f"総メソッド数: {self.df['Description'].nunique()}\n\n")
            
            # トップ問題
            f.write("🚨 主要パフォーマンス問題\n")
            f.write("-" * 30 + "\n")
            for _, issue in issues.head(10).iterrows():
                f.write(f"[{issue['Severity']}] {issue['Type']}: {issue['Method']}\n")
                f.write(f"  詳細: {issue['Issue']} ({issue['Value']})\n\n")
            
            # トップメソッド
            f.write("📊 高負荷メソッド Top10\n")
            f.write("-" * 30 + "\n")
            for i, (_, method) in enumerate(method_stats.head(10).iterrows(), 1):
                f.write(f"{i:2d}. {method['MethodName']}\n")
                f.write(f"    影響度: {method['AvgTotalPerFrameMs']:.2f}ms/frame ({method['ImpactPercentage']:.1f}%)\n")
                f.write(f"    呼び出し: {method['TotalCalls']} 回, スパイク: {method['SpikeCount']} 回\n\n")

    def run_full_analysis(self, output_dir='analysis_output'):
        """完全解析を実行"""
        print("🚀 CS1Profiler 完全解析を開始...")
        
        # 統計生成
        method_stats = self.method_statistics()
        frame_stats = self.frame_statistics()
        issues = self.detect_performance_issues(method_stats)
        
        # 可視化
        self.generate_visualizations(method_stats, frame_stats, output_dir)
        
        # エクスポート
        self.export_results(method_stats, frame_stats, issues, output_dir)
        
        # コンソール出力
        print("\n" + "="*60)
        print("🎯 CS1Profiler 解析結果サマリー")
        print("="*60)
        
        print(f"\n📊 パフォーマンス上位5メソッド:")
        for i, (_, method) in enumerate(method_stats.head(5).iterrows(), 1):
            print(f"{i}. {method['MethodName'][:50]}")
            print(f"   {method['AvgTotalPerFrameMs']:.2f}ms/frame ({method['ImpactPercentage']:.1f}%)")
        
        print(f"\n🚨 検出された問題: {len(issues)} 件")
        high_issues = issues[issues['Severity'] == 'HIGH']
        if len(high_issues) > 0:
            print("   高重要度問題:")
            for _, issue in high_issues.head(3).iterrows():
                print(f"   - {issue['Type']}: {issue['Method'][:40]}...")
        
        print(f"\n📁 詳細結果: {output_dir}/ フォルダに保存されました")
        print("   - method_statistics.csv: メソッド別統計")
        print("   - frame_statistics.csv: フレーム別統計") 
        print("   - performance_issues.csv: 検出された問題")
        print("   - analysis_report.txt: 解析レポート")
        print("   - *.png: 可視化グラフ")

def main():
    parser = argparse.ArgumentParser(description='CS1Profiler CSV Analysis Tool')
    parser.add_argument('csv_file', help='CS1ProfilerのCSVファイルパス')
    parser.add_argument('-o', '--output', default='analysis_output', help='出力ディレクトリ (デフォルト: analysis_output)')
    parser.add_argument('-s', '--spike-multiplier', type=float, default=2.0, help='スパイク検出の閾値倍率 (デフォルト: 2.0)')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.csv_file):
        print(f"❌ CSVファイルが見つかりません: {args.csv_file}")
        return
    
    try:
        analyzer = CS1ProfilerAnalyzer(args.csv_file)
        analyzer.run_full_analysis(args.output)
        print("\n✅ 解析完了!")
    except Exception as e:
        print(f"❌ 解析エラー: {e}")
        raise

if __name__ == '__main__':
    main()
