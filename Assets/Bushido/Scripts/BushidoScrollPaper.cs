using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Mesh scrollCurlMesh;
        Material scrollCurlMaterial;
        readonly Vector3[] scrollCurlVertices = new Vector3[130];
        readonly Vector2[] scrollCurlUV = new Vector2[130];
        readonly Color[] scrollCurlColors = new Color[130];

        void DrawCurvedScrollEdge(float seam, bool right, float open)
        {
            if (scrollCurlMesh == null)
            {
                scrollCurlMesh = new Mesh { name = "Hoja washi curvada" };
                scrollCurlMesh.MarkDynamic();
                var triangles = new int[64 * 6];
                for (int i = 0; i < 64; i++)
                {
                    int v = i * 2, t = i * 6;
                    triangles[t] = v; triangles[t+1] = v+1; triangles[t+2] = v+2;
                    triangles[t+3] = v+2; triangles[t+4] = v+1; triangles[t+5] = v+3;
                }
                scrollCurlMesh.vertices = scrollCurlVertices;
                scrollCurlMesh.triangles = triangles;
                scrollCurlMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                scrollCurlMaterial.SetInt("_Cull", 0);
                scrollCurlMaterial.SetInt("_ZWrite", 0);
                scrollCurlMaterial.SetInt("_ZTest", 8);
                titleAssets.Add(scrollCurlMesh);
                titleAssets.Add(scrollCurlMaterial);
            }
            float radius = Mathf.Lerp(32, 20, open);
            float direction = right ? 1 : -1;
            for (int i = 0; i <= 64; i++)
            {
                float u = i / 64f, angle = u * Mathf.PI * 1.8f;
                float z = radius * (1 - Mathf.Cos(angle));
                float x = seam + direction * (radius * Mathf.Sin(angle) + u * 8);
                float light = .62f + .38f * Mathf.Max(0, Mathf.Cos(angle - .65f));
                Color color = new Color(.92f * light, .83f * light, .65f * light, 1);
                // A small fibre variation is attached to the sheet, never to screen time.
                color *= 1f - .025f * Mathf.Sin(i * 2.7f);
                for (int end = 0; end < 2; end++)
                {
                    int v = i * 2 + end;
                    scrollCurlVertices[v] = new Vector3(x / 1280f * Screen.width,
                        (end == 0 ? 25f : 695f) / 720f * Screen.height, z * .001f);
                    scrollCurlUV[v] = new Vector2(u, end);
                    scrollCurlColors[v] = color;
                }
            }
            scrollCurlMesh.vertices = scrollCurlVertices;
            scrollCurlMesh.colors = scrollCurlColors;
            scrollCurlMesh.RecalculateBounds();
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            scrollCurlMaterial.SetPass(0);
            Graphics.DrawMeshNow(scrollCurlMesh, Matrix4x4.identity);
            GL.PopMatrix();
            DrawScrollFittings(seam + direction * 8f, radius);
        }
        void DrawScrollFittings(float x,float radius)
        {
            Color lacquer=new Color(.16f,.08f,.055f), brass=new Color(.72f,.53f,.26f);
            for(int end=0;end<2;end++)
            {
                float y=end==0?25f:695f, outward=end==0?-1f:1f;
                Panel(new Rect(x-7,y+outward*10-10,14,20),lacquer);
                Panel(new Rect(x-radius-5,y-5,radius*2+10,10),lacquer);
                Panel(new Rect(x-radius-3,y-3,radius*2+6,2),brass);
                Panel(new Rect(x-radius-3,y+2,radius*2+6,1),brass);
                float tip=y+outward*17;
                for(int row=-5;row<=5;row++)
                {
                    float w=Mathf.Sqrt(25-row*row);
                    Panel(new Rect(x-w,tip+row,w*2,1.1f),brass);
                }
                float bandY=end==0?34:666;
                Panel(new Rect(x-radius*.65f,bandY,radius*1.3f,20),new Color(.16f,.24f,.25f));
                // Repeating seigaiha-inspired semicircles on the indigo mounting.
                for(int wave=0;wave<3;wave++)
                for(int segment=0;segment<8;segment++)
                {
                    float a=Mathf.PI+segment*Mathf.PI/8, b=a+Mathf.PI/8;
                    Vector2 center=new Vector2(x+(wave-1)*radius*.4f,bandY+13);
                    Vector2 start=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*5;
                    Vector2 finish=center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*5;
                    Vector2 d=finish-start;
                    Matrix4x4 saved=GUI.matrix;
                    GUI.matrix=saved*Matrix4x4.TRS(start,Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);
                    Panel(new Rect(0,0,d.magnitude,1),brass);
                    GUI.matrix=saved;
                }
            }
        }    }
}

