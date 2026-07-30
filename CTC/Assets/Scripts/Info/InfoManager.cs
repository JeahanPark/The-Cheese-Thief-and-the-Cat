using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CTC.Runtime.Info
{
    /// <summary>
    /// CSV 데이터 테이블 보관소 (WES `InfoManager` 이식, 2026-07-31).
    ///
    /// 원본은 `Assets/Resources/CSVInfo/*.csv`, 스키마 클래스는 `Info/Generator/ConvertInfo/`에
    /// **자동 생성**된다(`Tools/CTC/Convert CSV → Info` 메뉴). 사람이 스키마 클래스를 손으로 고치지 않는다.
    ///
    /// **WES와 다른 점 하나** — WES는 `File.ReadAllLines("Assets/CSVInfo/…")`로 읽는데
    /// 빌드에는 `Assets/` 폴더가 없어 배포판에서 데이터가 통째로 비어 버린다.
    /// CTC는 배포용 빌드를 테스터에게 주므로 `Resources.Load<TextAsset>`로 읽는다 —
    /// 에디터·빌드·EditMode 테스트에서 모두 같은 경로로 동작한다.
    ///
    /// 로드는 **동기**다. 데이터가 4개 표뿐이고, 어떤 게임플레이 코드보다 먼저 값이 확정돼야 한다
    /// (비동기로 두면 첫 프레임에 가격이 0인 상점이 잠깐 보인다).
    /// </summary>
    public partial class InfoManager
    {
        private const string CSV_RESOURCE_DIR = "CSVInfo";
        private const char TYPE_HINT_SEPARATOR = '.';

        private static InfoManager s_Instance;

        /// <summary>첫 접근 시 자동 로드 — 씬·플레이모드·EditMode 테스트 어디서든 같은 값이 나온다.</summary>
        public static InfoManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new InfoManager();
                    s_Instance.LoadAllInfo();
                }
                return s_Instance;
            }
        }

        /// <summary>CSV를 고친 뒤 플레이 없이 다시 읽을 때(에디터 도구·테스트).</summary>
        public static void Reload()
        {
            s_Instance = null;
        }

        private List<T> LoadInfo<T>() where T : new()
        {
            var list = new List<T>();
            string path = $"{CSV_RESOURCE_DIR}/{typeof(T).Name}";

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[InfoManager] CSV 없음: Assets/Resources/{path}.csv");
                return list;
            }

            string[] lines = asset.text.Split('\n');
            if (lines.Length < 2)
            {
                Debug.LogWarning($"[InfoManager] {typeof(T).Name}: 데이터 행이 없다");
                return list;
            }

            // 헤더는 "필드명.타입힌트" — 타입힌트는 클래스 생성기용이라 로드 시엔 떼고 쓴다
            string[] headers = lines[0].Split(',');
            for (int i = 0; i < headers.Length; i++)
            {
                headers[i] = headers[i].Trim().Split(TYPE_HINT_SEPARATOR)[0];
            }

            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] values = line.Split(',');
                var item = new T();

                for (int col = 0; col < headers.Length && col < values.Length; col++)
                {
                    FieldInfo field = typeof(T).GetField(headers[col]);
                    if (field == null) continue;
                    field.SetValue(item, ParseValue(values[col].Trim(), field.FieldType, typeof(T).Name, headers[col]));
                }
                list.Add(item);
            }

            return list;
        }

        private static object ParseValue(string _value, Type _type, string _table, string _column)
        {
            try
            {
                if (_type == typeof(int)) return int.Parse(_value);
                if (_type == typeof(long)) return long.Parse(_value);
                if (_type == typeof(float)) return float.Parse(_value);
                if (_type == typeof(double)) return double.Parse(_value);
                if (_type == typeof(bool)) return _value == "1" || _value.ToLower() == "true";
                if (_type.IsEnum) return Enum.Parse(_type, _value);
                return _value;
            }
            catch (Exception e)
            {
                // 조용히 기본값을 넣으면 밸런스가 0이 된 채로 게임이 돌아간다 — 반드시 드러낸다
                Debug.LogError($"[InfoManager] {_table}.{_column} 파싱 실패: '{_value}' → {_type.Name} ({e.GetType().Name})");
                return _type.IsValueType ? Activator.CreateInstance(_type) : null;
            }
        }
    }
}
