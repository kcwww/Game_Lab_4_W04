using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 컴포넌트를 루트 오브젝트에 붙이면,
/// 자신과 모든 자식의 Transform을 저장/복원할 수 있습니다.
/// </summary>
public class TransformSnapshot : MonoBehaviour
{
    public enum SpaceMode { Local, World }

    [Header("저장 옵션")]
    public bool includeInactive = true;     // 비활성 자식도 포함
    public SpaceMode spaceMode = SpaceMode.Local; // 로컬/월드 기준

    // 내부 저장소: 경로 -> 데이터
    [SerializeField] private List<Record> _recordsList = new();  // 인스펙터 확인용
    private Dictionary<string, Record> _records;                 // 런타임 조회용

    [Serializable]
    public class Record
    {
        public string path;              // 루트 기준 경로: Name[idx]/Name[idx]/...
        public Vector3 position;         // spaceMode가 Local이면 localPosition, World면 worldPosition
        public Quaternion rotation;      // Local이면 localRotation, World면 worldRotation
        public Vector3 localScale;       // 항상 localScale로 저장/복원 (lossyScale은 set 불가)
    }

    // ====== Public API ======

    /// <summary> 현재 Transform 트리의 스냅샷을 메모리에 저장합니다. </summary>
    public void SaveSnapshot()
    {
        var list = new List<Record>(128);
        foreach (var t in GetComponentsInChildren<Transform>(includeInactive))
        {
            var rec = new Record
            {
                path = BuildPathRelativeToRoot(t, transform),
                localScale = t.localScale
            };

            if (spaceMode == SpaceMode.Local)
            {
                rec.position = t.localPosition;
                rec.rotation = t.localRotation;
            }
            else
            {
                rec.position = t.position;
                rec.rotation = t.rotation; // SetPositionAndRotation으로 복원
            }

            list.Add(rec);
        }

        _recordsList = list;                  // 인스펙터에도 보이게 유지
        _records = ToDict(list);              // 빠른 조회용
        // Debug.Log($"Snapshot saved: {_recordsList.Count} objects");
    }

    /// <summary> 저장된 스냅샷을 현재 Transform 트리에 적용(복원)합니다. </summary>
    public void LoadSnapshot()
    {
        if ((_recordsList == null || _recordsList.Count == 0) && (_records == null || _records.Count == 0))
        {
            Debug.LogWarning("TransformSnapshot: 저장된 스냅샷이 없습니다.");
            return;
        }

        if (_records == null || _records.Count == 0)
            _records = ToDict(_recordsList);

        int applied = 0, missed = 0;

        foreach (var rec in _recordsList)
        {
            var target = FindByPath(transform, rec.path);
            if (target == null)
            {
                missed++;
                continue;
            }

            if (spaceMode == SpaceMode.Local)
            {
                target.localPosition = rec.position;
                target.localRotation = rec.rotation;
                target.localScale = rec.localScale;
            }
            else
            {
                target.SetPositionAndRotation(rec.position, rec.rotation);
                target.localScale = rec.localScale; // 월드 스페이스여도 스케일은 로컬로 복원
            }

            applied++;
        }

        // Debug.Log($"Snapshot loaded. Applied: {applied}, Missed: {missed}");
    }

    /// <summary> JSON 문자열로 내보내기 (원하면 파일로 저장해 사용하세요) </summary>
    public string ExportJson()
    {
        var wrapper = new RecordWrapper { records = _recordsList.ToArray() };
        return JsonUtility.ToJson(wrapper, true);
    }

    /// <summary> JSON 문자열에서 불러와 현재 메모리 스냅샷으로 설정 </summary>
    public void ImportJson(string json)
    {
        var wrapper = JsonUtility.FromJson<RecordWrapper>(json);
        _recordsList = new List<Record>(wrapper.records ?? Array.Empty<Record>());
        _records = ToDict(_recordsList);
    }

    [Serializable] private class RecordWrapper { public Record[] records; }

    // ====== Helpers ======

    private static Dictionary<string, Record> ToDict(List<Record> list)
    {
        var dict = new Dictionary<string, Record>(list.Count);
        foreach (var r in list) dict[r.path] = r;
        return dict;
    }

    // 루트 기준 경로 생성: Name[idx]/Child[idx]/GrandChild[idx]
    private static string BuildPathRelativeToRoot(Transform t, Transform root)
    {
        var stack = new Stack<string>();
        var cur = t;
        while (cur != null)
        {
            string seg = $"{cur.name}[{cur.GetSiblingIndex()}]";
            stack.Push(seg);
            if (cur == root) break;
            cur = cur.parent;
        }
        return string.Join("/", stack);
    }

    // 경로로 Transform 찾기
    private static Transform FindByPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var segments = path.Split('/');
        Transform cur = null;

        // 첫 세그먼트는 항상 root 자신이어야 함
        if (!SegmentMatches(root, segments[0])) return null;
        cur = root;

        for (int i = 1; i < segments.Length; i++)
        {
            if (!TryParseSegment(segments[i], out string name, out int index))
                return null;

            // 같은 이름을 가진 형제들 중에서 "형제 인덱스"로 정확히 찾기
            // Transform.GetChild(index)는 전체 인덱스라 이름과 맞춰 체크 필요
            Transform next = null;
            // 빠르게: 전체 자식 중, GetSiblingIndex()가 index이고 name이 같은 것
            foreach (Transform child in cur)
            {
                if (child.name == name && child.GetSiblingIndex() == index)
                {
                    next = child;
                    break;
                }
            }
            if (next == null) return null;
            cur = next;
        }

        return cur;
    }

    private static bool SegmentMatches(Transform t, string segment)
    {
        if (!TryParseSegment(segment, out string name, out int index)) return false;
        return t.name == name && t.GetSiblingIndex() == index;
    }

    private static bool TryParseSegment(string seg, out string name, out int siblingIndex)
    {
        // "Name[3]" 형태 파싱
        name = seg;
        siblingIndex = -1;

        int lb = seg.LastIndexOf('[');
        int rb = seg.LastIndexOf(']');
        if (lb < 0 || rb < 0 || rb <= lb) return false;

        name = seg.Substring(0, lb);
        var idxStr = seg.Substring(lb + 1, rb - lb - 1);
        return int.TryParse(idxStr, out siblingIndex);
    }

    // ====== 데모용 단축키(선택) ======
#if UNITY_EDITOR
    private void Update()
    {
        // 예시: K=저장, L=불러오기
        if (Input.GetKeyDown(KeyCode.K)) SaveSnapshot();
        if (Input.GetKeyDown(KeyCode.L)) LoadSnapshot();
    }
#endif
}
