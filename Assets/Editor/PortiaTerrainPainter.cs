using UnityEngine;
using UnityEditor;

public class PortiaTerrainPainter : MonoBehaviour
{
    [MenuItem("Tools/Paint Portia Terrain")]
    static void Paint()
    {
        var t = Object.FindObjectOfType<Terrain>();
        var td = t.terrainData;
        int aR = td.alphamapResolution;
        int hR = td.heightmapResolution;
        
        Debug.Log($"Starting paint: aR={aR}, hR={hR}, layers={td.alphamapLayers}");
        
        float[,] h = td.GetHeights(0, 0, hR, hR);
        float[,,] alpha = new float[aR, aR, 4];
        int cx = aR / 2, cz = aR / 2;
        int flatR = Mathf.RoundToInt(aR * 0.22f);
        
        for (int z = 0; z < aR; z++)
        {
            for (int x = 0; x < aR; x++)
            {
                float dx = x - cx, dz = z - cz;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                
                int hx = x * (hR - 1) / (aR - 1);
                int hz = z * (hR - 1) / (aR - 1);
                
                float slope = 0;
                if (x > 0 && x < aR - 1 && z > 0 && z < aR - 1)
                {
                    int hx1 = (x + 1) * (hR - 1) / (aR - 1);
                    int hx0 = (x - 1) * (hR - 1) / (aR - 1);
                    int hz1 = (z + 1) * (hR - 1) / (aR - 1);
                    int hz0 = (z - 1) * (hR - 1) / (aR - 1);
                    float a = h[hz, hx1] - h[hz, hx0];
                    float b = h[hz1, hx] - h[hz0, hx];
                    slope = Mathf.Sqrt(a * a + b * b) * 40f;
                }
                
                float wF = 0, wG = 0, wD = 0, wS = 0;
                
                if (dist < flatR * 0.6f) { wF = 0.7f; wD = 0.3f; }
                else if (dist < flatR)
                {
                    float bl = (dist - flatR * 0.6f) / (flatR * 0.4f);
                    wF = (1f - bl) * 0.4f;
                    wG = 0.3f + bl * 0.3f;
                    wD = 0.2f;
                }
                else if (slope < 0.3f) { wG = 0.6f; wD = 0.3f; wS = 0.1f; }
                else if (slope < 0.8f) { wG = 0.3f; wD = 0.3f; wS = 0.4f; }
                else { wG = 0.1f; wD = 0.2f; wS = 0.7f; }
                
                // Dirt paths (cross shape)
                float pw = aR * 0.03f;
                bool onPathH = Mathf.Abs(dz) < pw && Mathf.Abs(dx) < flatR * 0.8f;
                bool onPathV = Mathf.Abs(dx) < pw && Mathf.Abs(dz) < flatR * 0.8f;
                if (onPathH || onPathV)
                {
                    wD = 0.85f;
                    wF *= 0.05f;
                    wG *= 0.05f;
                    wS *= 0.05f;
                }
                
                float tot = wF + wG + wD + wS;
                alpha[z, x, 0] = wF / tot;
                alpha[z, x, 1] = wG / tot;
                alpha[z, x, 2] = wD / tot;
                alpha[z, x, 3] = wS / tot;
            }
        }
        
        td.SetAlphamaps(0, 0, alpha);
        Debug.Log($"Portia terrain painted! {aR}x{aR} splatmap with 4 layers");
    }
}
