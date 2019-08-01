using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class GetVertexAndTri : MonoBehaviour
{
    #region Private variables 건들필요 없는 변수들
    int[] nVertex;
    int[] nTris;
    int[] EachByteSize;
    float[] levels;
    float[] times;

    List<float[,]> Vertex;  // Vertex[timestep][N, xyz]
    List<int[,]> Tris;      // Tris[timestep][N, xyzw]
    float[,] tVertex;       // tVertex[N, xyz]
    int[,] tTris;           // tTris[N, xyzw]

    MeshFilter filter;
    new MeshRenderer renderer;
    public MeshFilter Filter
    {
        get
        {
            if (filter == null)
            {
                filter = GetComponent<MeshFilter>();
            }
            return filter;
        }
    }
    public MeshRenderer Renderer
    {
        get
        {
            if (renderer == null)
            {
                renderer = GetComponent<MeshRenderer>();
            }
            return renderer;
        }
    }
    int _time;
    int _nlevel;
    #endregion

    // 시간과 iso level value를 화면에 표시하기 위한 UI 변수
    public Text CurTime;
    public Text IsoVal;

    // timestep 개수 미리 알아야함.
    int maxtimestep = 94;
    [Range(0,93)]
    public int timestep = 0;    //0부터 시작

    // iso level 개수 미리 알아야 함.
    [Range(-1,10)]
    public int nlevel = -1;  //1부터 시작, 0이면 iso level 전부 가시화, -1 이면 mesh null

    string isofilename = "FDS_evac_ch4-150_20190502_02";

    // Start is called before the first frame update
    void Start()
    {
        Vertex = new List<float[,]>();
        Tris = new List<int[,]>();
        (times, levels, nVertex, nTris, EachByteSize) = ReadPreInformationOfIso(isofilename, maxtimestep);

        _time = timestep;
        _nlevel = nlevel;

        IsoVal.text = "No isosurface";
        CurTime.text = "Time : 0 sec";
    }

        // Update is called once per frame
    void Update()
    {
        if(_time!=timestep || _nlevel!=nlevel)
        {
            // 
            (tVertex, tTris) = ReadVertexAndTriTimestep(EachByteSize, isofilename, timestep);
            
            if (tVertex != null && tTris != null)
            {
                Filter.sharedMesh = Build(nVertex[timestep], nTris[timestep], tVertex, tTris, nlevel);
            }
            _time = timestep;
            _nlevel = nlevel;

            CurTime.text = "Time : " + string.Format("{0:00.00}",times[timestep]) + " sec";
            if (nlevel == -1)
                IsoVal.text = "No isosurface";
            else if (nlevel == 0)
                IsoVal.text = "All isosurfaces are visualized.";
            else
                IsoVal.text = "Isosurface value : " + levels[nlevel];
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
            timestep++;
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            timestep--;
        if (Input.GetKeyDown(KeyCode.UpArrow))
            nlevel++;
        if (Input.GetKeyDown(KeyCode.DownArrow))
            nlevel--;
    }
    // Mesh를 만들어내는 함수 (vertex 개수, triangle 개수, vertex 좌표, triangle 인덱스, iso value)
    Mesh Build(int nvert, int ntris, float[,] vertex, int[,] tris, int targetlevel)
    {
        var mesh = new Mesh();

        //var hsize = size * 0.5f;
        if(targetlevel == -1)
        {
            mesh = null;
            return mesh;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        for (int i =0; i<nvert;i++)
        {
            vertices.Add(new Vector3(vertex[i, 0], vertex[i, 2], vertex[i, 1]));
            normals.Add(new Vector3(vertex[i, 0], vertex[i, 2], vertex[i, 1]));
            uv.Add(new Vector2(vertex[i, 0], vertex[i, 2]));
        }

        int ntris_level = 0;
        
        int j = 0;
        int[] triangles;

        if (targetlevel == 0)
        {
            triangles = new int[ntris * 3];   //level 전체 메쉬 가시화
            for (int i = 0; i < ntris; i++)
            {
                triangles[j] = tris[i, 0] - 1;
                triangles[j + 1] = tris[i, 2] - 1;
                triangles[j + 2] = tris[i, 1] - 1;
                j += 3;
            }
        }else
        {
            for (int i = 0; i < ntris; i++)
            {
                if (tris[i, 3] == targetlevel)
                {
                    ntris_level++;
                }
            }
            triangles = new int[ntris_level * 3]; // 해당 level만 메쉬 가시화
            for (int i = 0; i < ntris_level; i++)
            {
                if(tris[i,3] == targetlevel)
                {
                    triangles[j] = tris[i, 0] - 1;
                    triangles[j + 1] = tris[i, 1] - 1;
                    triangles[j + 2] = tris[i, 2] - 1;
                    j += 3;
                }
            }
        }            
            //if (j == triangles.Length)
            //{
            //    Debug.Log("Reading Triangle index done. ");
            //    break;
            //}                            
        
        Debug.Log("Triangles index : " + (j -3) +"  ,"  + "ntris*3  : " + triangles.Length/3 + "  , ntris (reading from iso file )  :  " + tris.LongLength);

        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray<Vector3>();
        //mesh.uv = uv;
        mesh.normals = normals.ToArray<Vector3>();
        mesh.triangles = triangles;

        Debug.Log("vertex : " + mesh.vertexCount + "  num of tri : " + mesh.triangles.Length + "  num of iso reading ntri_level : " + ntris_level);

        //mesh.RecalculateBounds();

        return mesh;
    }

    // *.iso 파일에서 target_timestep에 해당하는 vertex와 triangle을 읽음
    (float[,], int[,]) ReadVertexAndTriTimestep(int [] EachByteSize, string isofilename, int target_timestep)
    {
        float[,] tVertex;       // tVertex[N, xyz]
        int[,] tTris;           // tTris[N, xyzw]
        System.IO.BinaryReader reader = new System.IO.BinaryReader(File.Open(isofilename + ".iso", FileMode.Open, FileAccess.Read));

        if (target_timestep == 0)
            target_timestep = 1;
        //if(target_timestep == 1)
        //    reader.ReadBytes(EachByteSize[0]);

        
        for (int i = 0; i < target_timestep; i++)
        {
            reader.ReadBytes(EachByteSize[i]);
        }            

        // Target timestep 읽기 시작
        Debug.Log(" ================== timestep : " + target_timestep + " ================================ ");
        float[] times_local = new float[2];
        reader.ReadBytes(4);
        times_local[0] = reader.ReadSingle();
        times_local[1] = reader.ReadSingle();
        reader.ReadBytes(4);
        Debug.Log("current time : " + times_local[0] + " sec");

        int[] nvertfacesvolumes = new int[3];
        reader.ReadBytes(4);
        nvertfacesvolumes[0] = reader.ReadInt32();
        nvertfacesvolumes[1] = reader.ReadInt32();
        nvertfacesvolumes[2] = reader.ReadInt32();
        //Debug.Log("nvertfacesvolumes : " + nvertfacesvolumes[0] + ",  " + nvertfacesvolumes[1]);

        //Debug.Log(" Level  :  " + i + ", value : " + levels[i]);
        int nVertices = nvertfacesvolumes[0];

        int nTriangles = nvertfacesvolumes[1];

        Debug.Log("nVertices : " + nVertices + ", nTriangles : " + nTriangles);

        tVertex = new float[nVertices, 3];
        tTris = new int[nTriangles, 4];

        if (nVertices > 0 && nTriangles > 0)
        {
            // vertices
            Debug.Log("Print Vertices below...");

            reader.ReadBytes(4);

            for (int j = 0; j < nVertices; j++)
            {
                tVertex[j, 0] = reader.ReadSingle();
                tVertex[j, 1] = reader.ReadSingle();
                tVertex[j, 2] = reader.ReadSingle();
            }

            reader.ReadBytes(4);
            Debug.Log("");

            // triangles
            Debug.Log("Print Triangles below...");

            reader.ReadBytes(4);
            for (int j = 0; j < nTriangles; j++)
            {
                tTris[j, 0] = reader.ReadInt32();
                tTris[j, 1] = reader.ReadInt32();
                tTris[j, 2] = reader.ReadInt32();
            }

            reader.ReadBytes(4);
            Debug.Log("");

            // levels
            // mesh 1개당 id를 지정함. ==> surf_ind는 level value 임.
            reader.ReadBytes(4);
            for (int j = 0; j < nTriangles; j++)
            {
                tTris[j, 3] = reader.ReadInt32();
            }
            reader.ReadBytes(4);
        }
        if (target_timestep == 0 && nVertices == 0 && nTriangles == 0)
            reader.ReadBytes(16);

        //Vertex.Add(xyz);
        //Tris.Add(tris);
        Debug.Log("End of Reading" + target_timestep + " th timestep");
        //끝
        reader.Close();
        return (tVertex, tTris);
    }

    // *.iso 파일에서 모든 timestep의 vertex와 trangle을 읽음. 이 함수는 너무 느려서 사용 불가.
    (int[], int[], List<float[,]>, List<int[,]>) ReadVertexAndTri(string isofilename, int iframes, bool outfile, bool showcoord)
    {
        List<float[,]> Vertex = new List<float[,]>();
        List<int[,]> Tris = new List<int[,]>();
        int[] nVertex;
        int[] nTris;

        System.IO.BinaryReader reader = new System.IO.BinaryReader(File.Open(isofilename+".iso", FileMode.Open, FileAccess.Read));

        reader.ReadBytes(24);
        reader.ReadBytes(4);
        int nfloat = reader.ReadInt32();
        reader.ReadBytes(4);

        Debug.Log("nfloat : " + nfloat);

        float[] levels = new float[nfloat];
        if (nfloat > 0)
        {
            reader.ReadBytes(4);
            for (int i = 0; i < nfloat; i++)
            {
                levels[i] = reader.ReadSingle();
                Debug.Log("the value of level : " + levels[i]);
            }
            reader.ReadBytes(4);
        }

        reader.ReadBytes(4);
        int nint = reader.ReadInt32();
        reader.ReadBytes(4);
        Debug.Log("nint : " + nint);
        int[] nint_vals = new int[nint];
        if (nint > 0)
        {
            reader.ReadBytes(4);
            for (int i = 0; i < nint; i++)
            {
                levels[i] = reader.ReadInt32();
                Debug.Log("the value of int of " + i + ":" + nint_vals[i]);
            }
            reader.ReadBytes(4);
        }

        nVertex = new int[iframes];
        nTris = new int[iframes];

        for (int t = 0; t < iframes; t++)
        {
            Debug.Log(" ================== timestep : " + t + " ================================ ");
            float[] times_local = new float[2];
            reader.ReadBytes(4);
            times_local[0] = reader.ReadSingle();
            times_local[1] = reader.ReadSingle();
            reader.ReadBytes(4);
            Debug.Log("current time : " + times_local[0] + " sec");

            int[] nvertfacesvolumes = new int[3];
            reader.ReadBytes(4);
            nvertfacesvolumes[0] = reader.ReadInt32();
            nvertfacesvolumes[1] = reader.ReadInt32();
            nvertfacesvolumes[2] = reader.ReadInt32();
            Debug.Log("nvertfacesvolumes : " + nvertfacesvolumes[0] + ",  " + nvertfacesvolumes[1]);

            //Debug.Log(" Level  :  " + i + ", value : " + levels[i]);
            int nVertices = nvertfacesvolumes[0];

            int nTriangles = nvertfacesvolumes[1];

            Debug.Log("nVertices : " + nVertices + ", nTriangles : " + nTriangles);
            nVertex[t] = nVertices;
            nTris[t] = nTriangles;
            float[,] xyz = new float[nVertices, 3];
            int[,] tris = new int[nTriangles, 4];
            
            if (nVertices > 0 && nTriangles > 0)
            {
                // vertices
                Debug.Log("Print Vertices below...");
                
                reader.ReadBytes(4);
                //using (StreamWriter sw = new StreamWriter("outfile_" + t + "_" + times_local[0] + ".dat"))
                //{
                for (int j = 0; j < nVertices; j++)
                {
                    //int[] vertices = new short[3];
                    xyz[j, 0] = reader.ReadSingle();
                    xyz[j, 1] = reader.ReadSingle();
                    xyz[j, 2] = reader.ReadSingle();
                    if (showcoord)
                    {
                        if (j != 0) Console.Write(", ");
                        Console.Write("(" + xyz[j, 0] + ", " + xyz[j, 1] + ", " + xyz[j, 2] + ")");
                    }
                    //if (outfile)
                    //    sw.WriteLine(xyz[j, 0] + ", " + xyz[j, 1] + ", " + xyz[j, 2]);
                }

                reader.ReadBytes(4);
                Debug.Log("");

                // triangles
                Debug.Log("Print Triangles below...");
                
                reader.ReadBytes(4);
                for (int j = 0; j < nTriangles; j++)
                {
                    //int[] vertices = new short[3];
                    tris[j, 0] = reader.ReadInt32();
                    tris[j, 1] = reader.ReadInt32();
                    tris[j, 2] = reader.ReadInt32();

                    if (showcoord)
                    {
                        if (j != 0) Console.Write(", ");
                        Console.Write("(" + tris[j, 0] + ", " + tris[j, 1] + ", " + tris[j, 2] + ")");
                    }
                }

                reader.ReadBytes(4);
                Debug.Log("");

                // levels
                // mesh 1개당 id를 지정함. ==> surf_ind는 level value 임.
                reader.ReadBytes(4);
                for (int j = 0; j < nTriangles; j++)
                {
                    tris[j, 3] = reader.ReadInt32();
                    if (showcoord)
                    {
                        if (j != 0) Console.Write(", ");
                        Console.Write("nsurf_ind : " + tris[j, 3]);
                    }
                    //if (outfile)
                    //    sw.WriteLine(tris[j, 0] + ", " + tris[j, 1] + ", " + tris[j, 2] + ", " + tris[j, 3]);
                }
                    
                //}     //StreamWriter
                reader.ReadBytes(4);
            }
            if (t == 0 && nVertices == 0 && nTriangles == 0)
                reader.ReadBytes(16);

            Vertex.Add(xyz);
            Tris.Add(tris);
            Debug.Log("End of " + t +" th timestep");
        }
        //for(int u=0;u<1000;u++)
        //Debug.Log(reader.ReadInt32());
        
        reader.Close();
        return (nVertex, nTris, Vertex, Tris);
    }

    // *.iso 파일에서 가시화에 필요한 데이터를 미리 읽음. : timestep당 byte 사이즈, dt, 전체 시뮬레이션 시간, 점개수, 삼각형개수, iso level 몇개인지 등
    (float[], float[], int[], int[], int[]) ReadPreInformationOfIso(string isofilename, int maxTimeStep)
    {
        int[] EachByteSize = new int[maxTimeStep];
        float[] times = new float[maxtimestep];
        int timestep = 0;
        System.IO.BinaryReader reader = new System.IO.BinaryReader(File.Open(isofilename + ".iso", FileMode.Open, FileAccess.Read));

        for(int k = 0; k<EachByteSize.Length;k++)
            EachByteSize[k] = 0;

        reader.ReadBytes(28);
        EachByteSize[timestep] += 28;
        int nfloat = reader.ReadInt32();
        reader.ReadBytes(4);
        EachByteSize[timestep] += 8;

        //Debug.Log("nfloat : " + nfloat);

        float[] levels = new float[nfloat];
        if (nfloat > 0)
        {
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
            for (int i = 0; i < nfloat; i++)
            {
                levels[i] = reader.ReadSingle();
                EachByteSize[timestep] += 4;
                //Debug.Log("the value of level : " + levels[i]);
            }
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
        }

        reader.ReadBytes(4);
        EachByteSize[timestep] += 4;
        int nint = reader.ReadInt32();
        EachByteSize[timestep] += 4;
        reader.ReadBytes(4);
        EachByteSize[timestep] += 4;
        Debug.Log("nint : " + nint);
        int[] nint_vals = new int[nint];
        if (nint > 0)
        {
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
            for (int i = 0; i < nint; i++)
            {
                levels[i] = reader.ReadInt32();
                EachByteSize[timestep] += 4;
                Debug.Log("the value of int of " + i + ":" + nint_vals[i]);
            }
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
        }

        nVertex = new int[maxTimeStep];
        nTris = new int[maxTimeStep];

        for (int t = 0; t < maxTimeStep; t++)
        {
            timestep = t;
            Debug.Log(" ================== timestep : " + t + " ================================ ");
            float[] times_local = new float[2];
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
            times_local[0] = reader.ReadSingle();
            times_local[1] = reader.ReadSingle();
            times[timestep] = times_local[0];
            EachByteSize[timestep] += 8;
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
            Debug.Log("current time : " + times_local[0] + " sec");

            int[] nvertfacesvolumes = new int[3];
            reader.ReadBytes(4);
            EachByteSize[timestep] += 4;
            nvertfacesvolumes[0] = reader.ReadInt32();
            nvertfacesvolumes[1] = reader.ReadInt32();
            nvertfacesvolumes[2] = reader.ReadInt32();
            EachByteSize[timestep] += 12;
            Debug.Log("nvertfacesvolumes : " + nvertfacesvolumes[0] + ",  " + nvertfacesvolumes[1]);

            //Debug.Log(" Level  :  " + i + ", value : " + levels[i]);
            int nVertices = nvertfacesvolumes[0];

            int nTriangles = nvertfacesvolumes[1];

            Debug.Log("nVertices : " + nVertices + ", nTriangles : " + nTriangles);
            nVertex[t] = nVertices;
            nTris[t] = nTriangles;
            float[,] xyz = new float[nVertices, 3];
            int[,] tris = new int[nTriangles, 4];

            if (nVertices > 0 && nTriangles > 0)
            {
                // vertices
                Debug.Log("Print Vertices below...");

                reader.ReadBytes(4);
                EachByteSize[timestep] += 4;
                //using (StreamWriter sw = new StreamWriter("outfile_" + t + "_" + times_local[0] + ".dat"))
                //{
                for (int j = 0; j < nVertices; j++)
                {
                    //int[] vertices = new short[3];
                    xyz[j, 0] = reader.ReadSingle();
                    xyz[j, 1] = reader.ReadSingle();
                    xyz[j, 2] = reader.ReadSingle();
                    EachByteSize[timestep] += 12;
                    //if (showcoord)
                    //{
                    //    if (j != 0) Console.Write(", ");
                    //    Console.Write("(" + xyz[j, 0] + ", " + xyz[j, 1] + ", " + xyz[j, 2] + ")");
                    //}
                    //if (outfile)
                    //    sw.WriteLine(xyz[j, 0] + ", " + xyz[j, 1] + ", " + xyz[j, 2]);
                }

                reader.ReadBytes(4);
                EachByteSize[timestep] += 4;
                //Debug.Log("");

                //// triangles
                //Debug.Log("Print Triangles below...");

                reader.ReadBytes(4);
                EachByteSize[timestep] += 4;
                for (int j = 0; j < nTriangles; j++)
                {
                    //int[] vertices = new short[3];
                    tris[j, 0] = reader.ReadInt32();
                    tris[j, 1] = reader.ReadInt32();
                    tris[j, 2] = reader.ReadInt32();
                    EachByteSize[timestep] += 12;
                    //if (showcoord)
                    //{
                    //    if (j != 0) Console.Write(", ");
                    //    Console.Write("(" + tris[j, 0] + ", " + tris[j, 1] + ", " + tris[j, 2] + ")");
                    //}
                }

                reader.ReadBytes(4);
                EachByteSize[timestep] += 4;
                
                // levels
                // mesh 1개당 id를 지정함. ==> surf_ind는 level value 임.
                reader.ReadBytes(4);
                EachByteSize[timestep] += 4;
                for (int j = 0; j < nTriangles; j++)
                {
                    tris[j, 3] = reader.ReadInt32();
                    EachByteSize[timestep] += 4;
                    //if (showcoord)
                    //{
                    //    if (j != 0) Console.Write(", ");
                    //    Console.Write("nsurf_ind : " + tris[j, 3]);
                    //}
                    //if (outfile)
                    //    sw.WriteLine(tris[j, 0] + ", " + tris[j, 1] + ", " + tris[j, 2] + ", " + tris[j, 3]);
                }

                //}     //StreamWriter
                reader.ReadBytes(4);
                EachByteSize[timestep] += 4;
            }
            if (t == 0 && nVertices == 0 && nTriangles == 0)
            {
                reader.ReadBytes(16);
                EachByteSize[timestep] += 16;
            }

            Debug.Log("End of Reading ByteSize of timestep " + timestep + " : "+ EachByteSize[timestep]);
        }
        //for(int u=0;u<1000;u++)
        //Debug.Log(reader.ReadInt32());

        reader.Close();
        return (times,  levels ,nVertex, nTris, EachByteSize);
    }
}
