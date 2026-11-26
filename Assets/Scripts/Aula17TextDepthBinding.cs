using UnityEngine;

// Keep the legacy font's dynamic atlas on a local, depth-tested material.
[ExecuteAlways, RequireComponent(typeof(TextMesh))]
public sealed class Aula17TextDepthBinding : MonoBehaviour
{
    TextMesh textMesh;
    Renderer textRenderer;
    MaterialPropertyBlock properties;
    void OnEnable()
    {
        textMesh=GetComponent<TextMesh>();textRenderer=GetComponent<Renderer>();
        Font.textureRebuilt+=AtlasChanged;
        if(textMesh.font)textMesh.font.RequestCharactersInTexture(textMesh.text,textMesh.fontSize,textMesh.fontStyle);
        Bind();
    }
    void OnDisable(){Font.textureRebuilt-=AtlasChanged;}
    void AtlasChanged(Font font){if(textMesh&&font==textMesh.font)Bind();}
    void Bind()
    {
        if(!textMesh||!textRenderer||!textMesh.font||!textMesh.font.material)return;
        if(properties==null)properties=new MaterialPropertyBlock();
        textRenderer.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex",textMesh.font.material.mainTexture);
        textRenderer.SetPropertyBlock(properties);
    }
}
