using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Vaelith.Docencia2
{
    // One shared planar reflection per bathroom, only when its mirror is in view.
    [ExecuteAlways] public sealed class Docencia2Mirror : MonoBehaviour
    {
        public Renderer[] surfaces;
        public Vector3 normal;
        public Bounds viewingBounds;
        Camera reflection;
        RenderTexture texture;
        static bool rendering;
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=Render;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=Render;Cleanup();}
        void Cleanup(){if(reflection)DestroyImmediate(reflection.gameObject);if(texture){texture.Release();DestroyImmediate(texture);}}
        void Render(ScriptableRenderContext context,Camera source)
        {
            if(rendering||!isActiveAndEnabled||surfaces==null||surfaces.Length==0||!surfaces[0]||source.cameraType==CameraType.Reflection||source.cameraType==CameraType.Preview)return;
            if(!viewingBounds.Contains(source.transform.position))return;
            Vector3 point=surfaces[0].bounds.center+normal*.011f;
            if(Vector3.Dot(source.transform.position-point,normal)<=0)return;
            var planes=GeometryUtility.CalculateFrustumPlanes(source);bool visible=false;
            foreach(var s in surfaces)if(s&&s.enabled&&GeometryUtility.TestPlanesAABB(planes,s.bounds))visible=true;
            if(!visible)return;
            if(!reflection){var g=new GameObject("__PB_Mirror_RenderCamera"){hideFlags=HideFlags.HideAndDontSave};reflection=g.AddComponent<Camera>();reflection.enabled=false;g.AddComponent<UniversalAdditionalCameraData>();}
            int w=Mathf.Min(1536,Mathf.Max(256,source.pixelWidth*3/4)),h=Mathf.Max(128,Mathf.RoundToInt(w/source.aspect));
            if(!texture||texture.width!=w||texture.height!=h){if(texture){texture.Release();DestroyImmediate(texture);}texture=new RenderTexture(w,h,24,RenderTextureFormat.DefaultHDR){name="PB_Mirror_Temporary",hideFlags=HideFlags.HideAndDontSave,antiAliasing=2,filterMode=FilterMode.Bilinear};texture.Create();}
            reflection.CopyFrom(source);reflection.cullingMask|=(1<<28);reflection.allowMSAA=true;reflection.enabled=false;reflection.cameraType=CameraType.Reflection;reflection.targetTexture=texture;reflection.useOcclusionCulling=false;
            var data=reflection.GetUniversalAdditionalCameraData();data.renderShadows=false;data.renderPostProcessing=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            Vector3 n=normal.normalized;float d=-Vector3.Dot(n,point);
            var m=Matrix4x4.identity;
            m.m00=1-2*n.x*n.x;m.m01=-2*n.x*n.y;m.m02=-2*n.x*n.z;m.m03=-2*d*n.x;
            m.m10=-2*n.y*n.x;m.m11=1-2*n.y*n.y;m.m12=-2*n.y*n.z;m.m13=-2*d*n.y;
            m.m20=-2*n.z*n.x;m.m21=-2*n.z*n.y;m.m22=1-2*n.z*n.z;m.m23=-2*d*n.z;
            reflection.transform.position=m.MultiplyPoint(source.transform.position);
            reflection.transform.rotation=Quaternion.LookRotation(m.MultiplyVector(source.transform.forward),m.MultiplyVector(source.transform.up));
            reflection.worldToCameraMatrix=source.worldToCameraMatrix*m;
            Vector3 cp=reflection.worldToCameraMatrix.MultiplyPoint(point),cn=reflection.worldToCameraMatrix.MultiplyVector(n).normalized;
            reflection.projectionMatrix=source.CalculateObliqueMatrix(new Vector4(cn.x,cn.y,cn.z,-Vector3.Dot(cp,cn)));
            bool oldInvert=GL.invertCulling;rendering=true;
            try{
                foreach(var s in surfaces)if(s)s.enabled=false;
                GL.invertCulling=!oldInvert;
                #pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context,reflection);
                #pragma warning restore CS0618
                foreach(var s in surfaces)if(s){var props=new MaterialPropertyBlock();s.GetPropertyBlock(props);props.SetTexture("_MirrorTex",texture);s.SetPropertyBlock(props);}
            }finally{GL.invertCulling=oldInvert;foreach(var s in surfaces)if(s)s.enabled=true;rendering=false;}
        }
    }
}
