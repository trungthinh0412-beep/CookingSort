using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws the missing pieces and seams over the cover. No textures or objects are generated at runtime.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AlbumPuzzleGraphic : MaskableGraphic
{
    [SerializeField] private Color missingColor = new Color(.64f, .43f, .28f, 1);
    [SerializeField] private Color seamColor = new Color(.22f, .15f, .12f, .65f);
    [SerializeField, Min(.1f)] private float seamWidth = 2;
    private bool[] pieces = new bool[0];
    public void SetPieces(bool[] value) { pieces = value ?? new bool[0]; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (pieces.Length == 0) return;
        Rect rect = rectTransform.rect;
        int columns = 1;
        float best = float.MaxValue;
        for (int candidate = 1; candidate <= pieces.Length; candidate++)
        {
            if (pieces.Length % candidate != 0) continue;
            float score = Mathf.Abs(Mathf.Log((float)candidate / (pieces.Length / candidate) / Mathf.Max(.01f, rect.width / rect.height)));
            if (score < best) { columns = candidate; best = score; }
        }
        int rows = pieces.Length / columns;
        float w = rect.width / columns, h = rect.height / rows;
        float tab = Mathf.Min(w, h) * .16f;
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                float x = rect.xMin + col * w, y = rect.yMax - (row + 1) * h;
                var points = new List<Vector2>();
                // CCW shared edges, with deterministic matching tabs.
                Edge(points, new Vector2(x,y), new Vector2(x+w,y), row == rows-1 ? 0 : ((row+col)%2 == 0 ? tab : -tab));
                Edge(points, new Vector2(x+w,y), new Vector2(x+w,y+h), col == columns-1 ? 0 : ((row+col)%2 == 0 ? tab : -tab));
                Edge(points, new Vector2(x+w,y+h), new Vector2(x,y+h), row == 0 ? 0 : ((row+col)%2 == 0 ? tab : -tab));
                Edge(points, new Vector2(x,y+h), new Vector2(x,y), col == 0 ? 0 : ((row+col)%2 == 0 ? tab : -tab));
                if (!pieces[row * columns + col]) Fill(vh, points, missingColor);
                for (int i = 0; i < points.Count; i++) Line(vh, points[i], points[(i+1)%points.Count]);
            }
    }
    private static void Edge(List<Vector2> points, Vector2 a, Vector2 b, float tab)
    {
        var normal = new Vector2(-(b-a).y, (b-a).x).normalized;
        int samples = tab == 0 ? 1 : 20;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float bump = t > .32f && t < .68f ? Mathf.Sin((t-.32f)/.36f*Mathf.PI) : 0;
            points.Add(Vector2.Lerp(a,b,t) + normal * tab * bump);
        }
    }
    private void Line(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized * (seamWidth*.5f);
        int start = vh.currentVertCount;
        vh.AddVert(a-n, seamColor, Vector2.zero); vh.AddVert(a+n, seamColor, Vector2.zero);
        vh.AddVert(b+n, seamColor, Vector2.zero); vh.AddVert(b-n, seamColor, Vector2.zero);
        vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
    private static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;
    private static void Fill(VertexHelper vh, List<Vector2> p, Color c)
    {
        int start = vh.currentVertCount;
        var remaining = new List<int>();
        for (int i=0;i<p.Count;i++) { vh.AddVert(p[i],c,Vector2.zero); remaining.Add(i); }
        int guard = p.Count*p.Count;
        while (remaining.Count > 2 && guard-- > 0)
        {
            bool cut = false;
            for (int i=0;i<remaining.Count;i++)
            {
                int a=remaining[(i+remaining.Count-1)%remaining.Count], b=remaining[i], d=remaining[(i+1)%remaining.Count];
                if (Cross(p[b]-p[a],p[d]-p[b]) <= .001f) continue;
                bool inside = false;
                foreach (int j in remaining)
                {
                    if (j==a || j==b || j==d) continue;
                    if (Cross(p[b]-p[a],p[j]-p[a]) >= -.001f && Cross(p[d]-p[b],p[j]-p[b]) >= -.001f &&
                        Cross(p[a]-p[d],p[j]-p[d]) >= -.001f) { inside=true; break; }
                }
                if (inside) continue;
                vh.AddTriangle(start+a,start+b,start+d); remaining.RemoveAt(i); cut=true; break;
            }
            if (!cut) break;
        }
    }
}
