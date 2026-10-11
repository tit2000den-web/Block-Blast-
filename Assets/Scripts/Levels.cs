using System.Collections.Generic;
using UnityEngine;

// Dữ liệu các màn chơi của chế độ Adventure.
// Mỗi màn có điểm mục tiêu và các khối đá có sẵn trên bàn (sinh theo seed, lần nào cũng giống nhau).
public static class Levels
{
    public const int Count = 30;
    const string CompletedKey = "adv_completed";

    // Số màn đã qua (0 = chưa qua màn nào)
    public static int Completed
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(CompletedKey, 0), 0, Count);
        set
        {
            PlayerPrefs.SetInt(CompletedKey, Mathf.Clamp(value, 0, Count));
            PlayerPrefs.Save();
        }
    }

    // Màn cao nhất được phép chơi
    public static int Unlocked => Mathf.Min(Completed + 1, Count);

    public static bool IsUnlocked(int level) => level <= Unlocked;
    public static bool IsDone(int level) => level <= Completed;

    public static int Target(int level)
    {
        return 150 + level * 100 + (level / 5) * 150;
    }

    public static List<Vector2Int> Obstacles(int level)
    {
        const int size = BoardManager.Size;
        var rnd = new System.Random(level * 7919 + 13);
        var cells = new List<Vector2Int>();
        var used = new bool[size, size];
        var rowCount = new int[size];
        var colCount = new int[size];
        int count = Mathf.Min(2 + level, 22);
        bool mirror = level % 2 == 0;   // màn chẵn: xếp đối xứng trái-phải cho đẹp

        for (int attempt = 0; attempt < 400 && cells.Count < count; attempt++)
        {
            int r = rnd.Next(size);
            int c = rnd.Next(mirror ? size / 2 : size);
            if (!TryAdd(r, c)) continue;
            if (mirror && !TryAdd(r, size - 1 - c))
            {
                // bỏ ô vừa thêm để giữ đối xứng
                var last = cells[cells.Count - 1];
                cells.RemoveAt(cells.Count - 1);
                used[last.x, last.y] = false;
                rowCount[last.x]--;
                colCount[last.y]--;
            }
        }
        return cells;

        // Không cho hàng/cột nào gần đầy (tránh tự xóa ngay hoặc quá khó)
        bool TryAdd(int r, int c)
        {
            if (used[r, c] || rowCount[r] >= size - 3 || colCount[c] >= size - 3) return false;
            used[r, c] = true;
            rowCount[r]++;
            colCount[c]++;
            cells.Add(new Vector2Int(r, c));
            return true;
        }
    }
}
