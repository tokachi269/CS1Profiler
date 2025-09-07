import struct
import os
import sys

def analyze_cgs_file(filepath):
    """CGSファイル（Cities: Skylines設定ファイル）の構造を解析"""
    
    if not os.path.exists(filepath):
        print(f"File not found: {filepath}")
        return
    
    with open(filepath, 'rb') as f:
        data = f.read()
    
    print(f"File size: {len(data):,} bytes")
    
    # ヘッダー解析
    if len(data) < 10:
        print("File too small")
        return
    
    header = data[0:4].decode('ascii', errors='ignore')
    version = struct.unpack('<H', data[4:6])[0]
    
    print(f"Header: {header}")
    print(f"Version: {version}")
    
    pos = 6
    
    # Int値のセクション
    if pos + 4 <= len(data):
        int_count = struct.unpack('<I', data[pos:pos+4])[0]
        print(f"Int values count: {int_count:,}")
        pos += 4
        
        if int_count > 100000:  # 異常に大きい場合はエラー
            print(f"ERROR: Invalid int_count: {int_count}")
            return
        
        # Int値を読む（最初の10個まで）
        for i in range(min(int_count, 10)):
            if pos + 4 > len(data):
                break
            key_len = struct.unpack('<I', data[pos:pos+4])[0]
            pos += 4
            
            if key_len > 1000 or pos + key_len > len(data):
                print(f"  [Int {i}] Invalid key length: {key_len}")
                break
                
            key = data[pos:pos+key_len].decode('utf-8', errors='ignore')
            pos += key_len
            
            if pos + 4 > len(data):
                break
            value = struct.unpack('<i', data[pos:pos+4])[0]
            pos += 4
            
            print(f"  [Int {i}] {key} = {value}")
        
        # 残りをスキップ
        for i in range(int_count - min(int_count, 10)):
            if pos + 4 > len(data):
                break
            key_len = struct.unpack('<I', data[pos:pos+4])[0]
            pos += 4 + key_len + 4
            if pos > len(data):
                break
    
    # Bool値のセクション
    if pos + 4 <= len(data):
        bool_count = struct.unpack('<I', data[pos:pos+4])[0]
        print(f"Bool values count: {bool_count:,}")
        pos += 4
        
        # Bool値の詳細をスキップして次のセクションへ
        for i in range(bool_count):
            if pos + 4 > len(data):
                break
            key_len = struct.unpack('<I', data[pos:pos+4])[0]
            pos += 4 + key_len + 1  # bool は1バイト
            if pos > len(data):
                break
    
    # Float値のセクション
    if pos + 4 <= len(data):
        float_count = struct.unpack('<I', data[pos:pos+4])[0]
        print(f"Float values count: {float_count:,}")
        pos += 4
        
        # Float値をスキップ
        for i in range(float_count):
            if pos + 4 > len(data):
                break
            key_len = struct.unpack('<I', data[pos:pos+4])[0]
            pos += 4 + key_len + 4  # float は4バイト
            if pos > len(data):
                break
    
    # String値のセクション
    if pos + 4 <= len(data):
        string_count = struct.unpack('<I', data[pos:pos+4])[0]
        print(f"String values count: {string_count:,}")
        pos += 4
        
        large_strings = []
        total_string_size = 0
        
        # String値を読む（最初の20個まで）
        for i in range(min(string_count, 20)):
            if pos + 4 > len(data):
                break
            key_len = struct.unpack('<I', data[pos:pos+4])[0]
            pos += 4
            
            if key_len > 1000 or pos + key_len > len(data):
                print(f"  [String {i}] Invalid key length: {key_len}")
                break
                
            key = data[pos:pos+key_len].decode('utf-8', errors='ignore')
            pos += key_len
            
            if pos + 4 > len(data):
                break
            value_len = struct.unpack('<I', data[pos:pos+4])[0]
            pos += 4
            
            if value_len > len(data) or pos + value_len > len(data):
                print(f"  [String {i}] Invalid value length: {value_len:,}")
                break
                
            value = data[pos:pos+value_len].decode('utf-8', errors='ignore')
            pos += value_len
            total_string_size += value_len
            
            if value_len > 1000:  # 大きな文字列を記録
                large_strings.append((key, value_len, value[:100] + "..." if len(value) > 100 else value))
                print(f"  [String {i}] {key} = {value_len:,} bytes: {value[:50]}...")
            else:
                print(f"  [String {i}] {key} = {value}")
        
        print(f"Total string data size: {total_string_size:,} bytes")
        
        if large_strings:
            print("\nLarge strings (>1KB):")
            for key, size, preview in large_strings:
                print(f"  {key}: {size:,} bytes - {preview}")
    
    print(f"Parsed up to position: {pos:,} / {len(data):,} bytes")

if __name__ == "__main__":
    # Windows環境でのuserGameState.cgsファイルパス
    import os
    appdata = os.environ.get('LOCALAPPDATA', '')
    filepath = os.path.join(appdata, 'Colossal Order', 'Cities_Skylines', 'userGameState.cgs')
    
    if len(sys.argv) > 1:
        filepath = sys.argv[1]
    
    analyze_cgs_file(filepath)
