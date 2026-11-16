using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AutoSceneVR
{
    public class ReconstructionVerification : MonoBehaviour
    {
        [Serializable] class Result {
            public bool passed,frames_advance,surface_meshes_loaded,tv_left_of_cabinet,source_view_nonblank;
            public int surface_groups,vertices,triangles;
            public string scope="Standalone visible-surface import, placement and rendering; no hidden-geometry claim";
        }
        IEnumerator Start()
        {
            string folder=SimulationController.Argument("--verify-reconstruction");
            if(string.IsNullOrEmpty(folder)) yield break;
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(1);
            int start=Time.frameCount;
            yield return new WaitForSecondsRealtime(.3f);
            var targets=FindObjectsByType<ReconstructedObject>();
            var filters=FindObjectsByType<MeshFilter>();
            var tv=targets.FirstOrDefault(o=>o.snapshotId=="tv_01");
            var cabinet=targets.FirstOrDefault(o=>o.snapshotId=="cabinet_01");
            var nav=GetComponent<ReconstructionNavigation>();
            var image=Capture(nav.sourceCamera,Path.Combine(folder,"reference-view.png"));
            var pixels=image.GetPixels32();
            int minimum=pixels.Min(p=>(int)p.r+p.g+p.b),maximum=pixels.Max(p=>(int)p.r+p.g+p.b);
            var result=new Result {
                frames_advance=Time.frameCount>start,
                surface_groups=targets.Length,
                vertices=filters.Sum(m=>m.sharedMesh.vertexCount),
                triangles=filters.Sum(m=>m.sharedMesh.triangles.Length/3),
                surface_meshes_loaded=targets.Length>=10 && filters.All(m=>m.sharedMesh.vertexCount>0),
                tv_left_of_cabinet=tv!=null && cabinet!=null && nav.sourceCamera.WorldToViewportPoint(tv.transform.position).x<nav.sourceCamera.WorldToViewportPoint(cabinet.transform.position).x,
                source_view_nonblank=maximum-minimum>100
            };
            result.passed=result.frames_advance&&result.surface_meshes_loaded&&result.tv_left_of_cabinet&&result.source_view_nonblank;
            File.WriteAllText(Path.Combine(folder,"runtime-validation.json"),JsonUtility.ToJson(result,true));
            Destroy(image);
            Application.Quit(result.passed?0:1);
        }
        static Texture2D Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(640,426,24,RenderTextureFormat.ARGB32);rt.Create();
            var old=RenderTexture.active;camera.aspect=640f/426;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;
            var texture=new Texture2D(640,426,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,640,426),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=old;rt.Release();Destroy(rt);
            return texture;
        }
    }
}
