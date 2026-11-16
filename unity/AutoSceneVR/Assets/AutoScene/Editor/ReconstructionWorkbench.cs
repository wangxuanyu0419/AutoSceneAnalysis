using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AutoSceneVR.Editor
{
    public static class ReconstructionWorkbench
    {
        const string Generated = "Assets/ReconstructionGenerated";
        [Serializable] public class V3 { public float x,y,z; public Vector3 Value => new Vector3(x,y,z); public static V3 Of(Vector3 p) => new V3{x=p.x,y=p.y,z=p.z}; }
        [Serializable] class CameraRecord { public V3 position,forward,up; public float fov_y_deg; }
        [Serializable] class ObjectRecord { public string id,label,identity_evidence; public V3 position,size; }
        [Serializable] class SceneRecord { public string scene_version,texture,surfaces; public int[] image_size_px; public CameraRecord camera; public ObjectRecord[] objects; }
        [Serializable] class Calibration { public int width,height; public float fov_x_deg,fov_y_deg,camera_height_m; public string input_scope = "Camera calibration only; height is an optional measured input; object geometry remains private to evaluation"; }
        [Serializable] class TruthObject { public string id,label; public V3 center_camera; public float[] bbox_xyxy_px; }
        [Serializable] class Truth { public int seed; public string split; public TruthObject[] objects; public string depth_file="truth-depth.bin"; public int depth_width=160,depth_height=106; }
        static readonly List<Tuple<GameObject,string>> benchObjects = new List<Tuple<GameObject,string>>();

        public static string Import(string sceneJson)
        {
            var record = JsonUtility.FromJson<SceneRecord>(File.ReadAllText(sceneJson));
            string folder = Path.GetDirectoryName(sceneJson);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
            string texturePath = Generated+"/reference.png";
            File.Copy(Path.Combine(folder,record.texture),texturePath,true);
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.textureCompression=TextureImporterCompression.Uncompressed;
            textureImporter.npotScale=TextureImporterNPOTScale.None;
            textureImporter.filterMode=FilterMode.Point; textureImporter.wrapMode=TextureWrapMode.Clamp;
            textureImporter.mipmapEnabled=false; textureImporter.maxTextureSize=4096; textureImporter.SaveAndReimport();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Cull",0);
            material=ReplaceAsset(material,Generated+"/PhotoSurface.mat");
            var root = new GameObject("Image-grounded room — "+record.scene_version);
            using(var reader=new BinaryReader(File.OpenRead(Path.Combine(folder,record.surfaces)))) {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4))!="ASR1") throw new Exception("Unknown mesh packet");
                int count=reader.ReadInt32();
                for(int i=0;i<count;i++) {
                    string id=Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                    int nv=reader.ReadInt32(),ni=reader.ReadInt32();
                    var vertices=new Vector3[nv]; var uv=new Vector2[nv]; var indices=new int[ni];
                    for(int j=0;j<nv;j++) { vertices[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()); uv[j]=new Vector2(reader.ReadSingle(),reader.ReadSingle()); }
                    for(int j=0;j<ni;j++) indices[j]=reader.ReadInt32();
                    var item=record.objects.First(o=>o.id==id);
                    var go=new GameObject(id+" · "+item.label); go.transform.SetParent(root.transform); go.transform.position=item.position.Value;
                    var evidence=go.AddComponent<ReconstructedObject>(); evidence.snapshotId=id; evidence.identityEvidence=item.identity_evidence;
                    if(nv==0) continue;
                    var mesh=new Mesh{name=id,indexFormat=IndexFormat.UInt32}; mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    mesh=ReplaceAsset(mesh,Generated+"/"+id+".asset");
                    go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=material;
                    if(id!="background") go.AddComponent<MeshCollider>().sharedMesh=mesh;
                }
            }
            var camera=new GameObject("Reference camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(record.camera.position.Value,Quaternion.LookRotation(record.camera.forward.Value,record.camera.up.Value));
            camera.fieldOfView=record.camera.fov_y_deg; camera.nearClipPlane=.02f; camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.14f,.17f);
            camera.tag="MainCamera"; camera.gameObject.AddComponent<AudioListener>();
            var bounds=new Bounds(Vector3.zero,Vector3.one);
            foreach(var r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var overview=new GameObject("Geometry overview camera").AddComponent<Camera>();
            overview.transform.position=bounds.center+new Vector3(bounds.size.x*.65f,bounds.size.y+2,-bounds.size.z*.7f-2);
            overview.transform.LookAt(bounds.center); overview.fieldOfView=60; overview.farClipPlane=100;
            overview.clearFlags=CameraClearFlags.SolidColor; overview.backgroundColor=new Color(.12f,.14f,.17f);
            overview.gameObject.SetActive(false);
            var nav=root.AddComponent<ReconstructionNavigation>(); nav.sourceCamera=camera; nav.overviewCamera=overview;
            root.AddComponent<ReconstructionVerification>();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(root.scene,Generated+"/ReconstructedRoom.unity");
            Capture(camera,Path.Combine(folder,"unity-reference-view.png"),record.image_size_px[0],record.image_size_px[1]);
            Vector3 old=camera.transform.position;
            camera.transform.position+=camera.transform.right*.5f;
            Capture(camera,Path.Combine(folder,"unity-offset-view.png"),record.image_size_px[0],record.image_size_px[1]);
            camera.transform.position=old;
            Capture(overview,Path.Combine(folder,"unity-geometry-overview.png"),960,640);
            return "Imported "+record.objects.Length+" surface groups into "+Generated;
        }

        public static string BuildWindows()
        {
            string destination=Path.GetFullPath("Builds/Reconstruction/RoomReconstruction.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Generated+"/ReconstructedRoom.unity"},locationPathName=destination,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Reconstruction build failed");
            return destination;
        }

        static T ReplaceAsset<T>(T asset,string path) where T : UnityEngine.Object
        {
            // Native mesh buffers must be recreated. CopySerialized can retain GPU
            // buffers from the previous room when topology changes between imports.
            if(!path.StartsWith(Generated+"/",StringComparison.Ordinal)) throw new Exception("Generated assets only");
            if(AssetDatabase.LoadMainAssetAtPath(path)!=null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset,path);
            return asset;
        }

        public static void Capture(Camera camera,string path,int width,int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32); rt.Create();
            var old=RenderTexture.active; bool active=camera.gameObject.activeSelf; camera.gameObject.SetActive(true);
            camera.aspect=(float)width/height;
            // The first request after scene import initializes URP's renderer caches.
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=old; camera.gameObject.SetActive(active); rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
        }

        public static string GenerateBenchmark(string output,int count=8)
        {
            string previous=EditorSceneManager.GetActiveScene().path;
            for(int index=0;index<count;index++) {
                int seed=(index<count/2?100:200)+index%(count/2);
                var rng=new System.Random(seed);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); benchObjects.Clear();
                string dir=Path.Combine(output,"scene-"+seed); Directory.CreateDirectory(dir);
                var floor=Material("floor",new Color(.22f+F(rng)*.2f,.12f+F(rng)*.13f,.06f+F(rng)*.10f));
                // New texture per room: changes board spacing, tint and noise independently of geometry.
                var tex=new Texture2D(128,128); var colors=new Color[128*128];
                for(int y=0;y<128;y++) for(int x=0;x<128;x++) colors[y*128+x]=floor.color*(.75f+.25f*F(rng))*((y%16==0||x%64==0)?.7f:1);
                tex.SetPixels(colors); tex.Apply(); floor.SetTexture("_BaseMap",tex); floor.SetTextureScale("_BaseMap",new Vector2(8,8));
                var plaster=Material("plaster",Color.Lerp(new Color(.72f,.66f,.52f),new Color(.55f,.68f,.73f),F(rng)));
                var timber=Material("wood",new Color(.25f+F(rng)*.3f,.15f+F(rng)*.2f,.08f+F(rng)*.12f));
                var black=Material("black",new Color(.025f,.035f,.045f)); var white=Material("ivory",new Color(.85f,.81f,.69f));
                Box("floor",null,new Vector3(0,-.1f,0),new Vector3(9,.2f,12),floor);
                Box("back wall",null,new Vector3(0,1.65f,4.1f),new Vector3(9,3.3f,.15f),plaster);
                Box("right wall",null,new Vector3(4.5f,1.65f,0),new Vector3(.15f,3.3f,8.2f),plaster);
                Box("left wall",null,new Vector3(-4.5f,1.65f,0),new Vector3(.15f,3.3f,8.2f),plaster);
                for(int i=0;i<3;i++) Box("window",null,new Vector3(-1.3f+i*1.2f,2.1f,3.99f),new Vector3(.8f,1.4f,.025f),Material("glass",new Color(.6f,.77f,.81f)));
                float side=(seed%2==0?1:-1);
                float tvX=side*(-2.8f+F(rng)*.45f),cabX=side*(2.3f+F(rng)*.5f);
                var tv=Entity("tv","television",new Vector3(tvX,.98f+F(rng)*.2f,3.7f-F(rng)*.5f));
                Box("TV bezel",tv,new Vector3(0,0,0),new Vector3(1.5f,.85f,.12f),black);
                Box("screen",tv,new Vector3(0,0,-.071f),new Vector3(1.39f,.74f,.025f),Material("screen",new Color(.055f,.11f,.14f)));
                Box("TV foot",tv,new Vector3(0,-.48f,0),new Vector3(.45f,.12f,.3f),black);
                Box("console",null,new Vector3(tvX,.35f,tv.position.z+.05f),new Vector3(2,.65f,.6f),white);
                var cabinet=Entity("cabinet","cabinet",new Vector3(cabX,1.05f+F(rng)*.1f,3.65f-F(rng)*.4f));
                Box("case",cabinet,Vector3.zero,new Vector3(1.3f,2.1f,.65f),timber);
                foreach(float x in new[]{-.32f,.32f}) {
                    Box("door",cabinet,new Vector3(x,0,-.34f),new Vector3(.62f,2.0f,.035f),white);
                    Box("handle",cabinet,new Vector3(x>0?.1f:-.1f,0,-.38f),new Vector3(.035f,.26f,.04f),black);
                }
                var table=Entity("table","table",new Vector3(-.8f+F(rng)*1.5f,0,.7f+F(rng)*.6f));
                float tw=1.4f+F(rng)*.5f,td=.9f+F(rng)*.4f;
                Box("top",table,new Vector3(0,.78f,0),new Vector3(tw,.09f,td),timber);
                foreach(float x in new[]{-tw*.4f,tw*.4f}) foreach(float z in new[]{-td*.35f,td*.35f}) Box("leg",table,new Vector3(x,.37f,z),new Vector3(.075f,.74f,.075f),timber);
                for(int i=0;i<2;i++) {
                    var chair=Entity("chair-"+i,"chair",table.position+new Vector3(i==0?-tw*.8f:tw*.8f,0,0));
                    Box("seat",chair,new Vector3(0,.46f,0),new Vector3(.5f,.07f,.5f),timber);
                    Box("back",chair,new Vector3(0,.79f,.24f),new Vector3(.5f,.6f,.055f),timber);
                    foreach(float x in new[]{-.19f,.19f}) foreach(float z in new[]{-.19f,.19f}) Box("chair leg",chair,new Vector3(x,.23f,z),new Vector3(.045f,.46f,.045f),black);
                }
                var lamp=Entity("lamp","lamp",new Vector3(table.position.x,2.6f,table.position.z));
                Box("shade",lamp,Vector3.zero,new Vector3(.55f,.17f,.36f),white);
                Box("cord",lamp,new Vector3(0,.28f,0),new Vector3(.015f,.5f,.015f),black);
                var sun=new GameObject("daylight").AddComponent<Light>(); sun.type=LightType.Directional;
                sun.intensity=.8f+F(rng)*1.3f; sun.transform.rotation=Quaternion.Euler(35+F(rng)*35,-70+F(rng)*120,0); sun.shadows=LightShadows.Soft;
                RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=Color.white*(.28f+F(rng)*.3f);
                var camera=new GameObject("benchmark camera").AddComponent<Camera>(); camera.fieldOfView=55; camera.aspect=640f/426;
                camera.transform.position=new Vector3(-.25f+F(rng)*.5f,1.5f+F(rng)*.2f,-4.5f);
                camera.transform.LookAt(new Vector3(0,1.3f,2.8f)); camera.nearClipPlane=.05f; camera.farClipPlane=40;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.5f,.6f,.7f);
                Physics.SyncTransforms();
                Capture(camera,Path.Combine(dir,"rgb.png"),640,426);
                var calibration=new Calibration{width=640,height=426,fov_y_deg=camera.fieldOfView,fov_x_deg=Camera.VerticalToHorizontalFieldOfView(camera.fieldOfView,camera.aspect),camera_height_m=camera.transform.position.y};
                File.WriteAllText(Path.Combine(dir,"camera.json"),JsonUtility.ToJson(calibration,true));
                var truth=new Truth{seed=seed,split=index<count/2?"development":"held_out",objects=benchObjects.Select(pair=>TruthFor(pair.Item1,pair.Item2,camera)).ToArray()};
                File.WriteAllText(Path.Combine(dir,"truth.json"),JsonUtility.ToJson(truth,true));
                using(var writer=new BinaryWriter(File.Open(Path.Combine(dir,"truth-depth.bin"),FileMode.Create))) {
                    writer.Write(truth.depth_width); writer.Write(truth.depth_height);
                    for(int y=0;y<truth.depth_height;y++) for(int x=0;x<truth.depth_width;x++) {
                        var ray=camera.ViewportPointToRay(new Vector3((x+.5f)/truth.depth_width,1-(y+.5f)/truth.depth_height,0));
                        writer.Write(Physics.Raycast(ray,out RaycastHit hit,40)?camera.transform.InverseTransformPoint(hit.point).z:0);
                    }
                }
            }
            if(!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            return "Generated "+count+" RGB scenes with separate calibration and private evaluation truth";
        }

        static TruthObject TruthFor(GameObject go,string label,Camera camera)
        {
            var bounds=new Bounds(go.transform.position,Vector3.zero);
            foreach(var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Vector2 lo=new Vector2(float.MaxValue,float.MaxValue),hi=new Vector2(float.MinValue,float.MinValue);
            for(int a=0;a<8;a++) {
                Vector3 p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((a&1)==0?-1:1,(a&2)==0?-1:1,(a&4)==0?-1:1));
                Vector3 v=camera.WorldToViewportPoint(p); Vector2 pixel=new Vector2(v.x*640,(1-v.y)*426);
                lo=Vector2.Min(lo,pixel); hi=Vector2.Max(hi,pixel);
            }
            return new TruthObject{id=go.name,label=label,center_camera=V3.Of(camera.transform.InverseTransformPoint(bounds.center)),bbox_xyxy_px=new[]{lo.x,lo.y,hi.x,hi.y}};
        }
        static Transform Entity(string id,string label,Vector3 position) { var go=new GameObject(id); go.transform.position=position; benchObjects.Add(Tuple.Create(go,label)); return go.transform; }
        static Material Material(string name,Color color) { var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name; m.color=color; return m; }
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material m) { var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=p; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=m; return go; }
        static float F(System.Random random) => (float)random.NextDouble();
    }
}
