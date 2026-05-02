using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可 JSON 序列化的管道关卡定义。
/// </summary>
[Serializable]
public class LevelData
{
    public string id = "level_1";
    public string displayName = "关卡 1";
    [Tooltip("步数限制（>0 时启用挑战，限定步数内通关得 1 颗星）；0 表示无限制")]
    public int moveLimit = 0;
    public List<CellData> cells = new List<CellData>();

    [Serializable]
    public struct CellData
    {
        public Vector3Int cubieCoord;
        public Vector3Int faceNormal;
        public int kind;        // PipeKind
        public int orientation; // 0..3
        public int portalGroup; // 传送门配对组号
    }
}
