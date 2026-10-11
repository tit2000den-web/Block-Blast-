using System.Collections.Generic;
using UnityEngine;

// Danh sách hình khối. '#' = có ô, '.' = trống, '|' = xuống hàng.
// Mỗi ô là Vector2Int(x = hàng, y = cột), giống BoardManager.
public static class BlockShapes
{
    static readonly string[] Patterns =
    {
        // Ô đơn
        "#",
        // Thanh thẳng
        "##", "#|#",
        "###", "#|#|#",
        "####", "#|#|#|#",
        "#####", "#|#|#|#|#",
        // Vuông / chữ nhật
        "##|##",
        "###|###|###",
        "###|###", "##|##|##",
        // L nhỏ (3 ô)
        "#.|##", "##|#.", "##|.#", ".#|##",
        // L (4 ô)
        "#.|#.|##", "###|#..", "##|.#|.#", "..#|###",
        // J (4 ô)
        ".#|.#|##", "#..|###", "##|#.|#.", "###|..#",
        // T
        "###|.#.", ".#.|###", "#.|##|#.", ".#|##|.#",
        // S / Z
        ".##|##.", "#.|##|.#", "##.|.##", ".#|##|#.",
        // L lớn (5 ô)
        "#..|#..|###", "###|#..|#..", "###|..#|..#", "..#|..#|###",
    };

    public static readonly Vector2Int[][] All = Parse();

    public static Vector2Int[] Random()
    {
        return All[UnityEngine.Random.Range(0, All.Length)];
    }

    static Vector2Int[][] Parse()
    {
        var result = new Vector2Int[Patterns.Length][];
        for (int i = 0; i < Patterns.Length; i++)
        {
            var cells = new List<Vector2Int>();
            string[] rows = Patterns[i].Split('|');
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                    if (rows[r][c] == '#') cells.Add(new Vector2Int(r, c));
            result[i] = cells.ToArray();
        }
        return result;
    }
}
