using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using WesternLemegeton;

public sealed class MotionTextureImport:AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/Motion/")||!assetPath.EndsWith(".png"))return;
        var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;
        importer.isReadable=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
        importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
}

public static class MotionSetup
{
    public static void Validate()
    {
        var texture=Resources.Load<Texture2D>("Motion/HunterSkills");
        if(!texture||!texture.isReadable)throw new System.Exception("Readable hunter motion atlas required");
        if(texture.width%6!=0||texture.height%4!=0)throw new System.Exception("Atlas must contain 6 x 4 equal cells");
        if(HunterAtlas.IsInk(texture.GetPixels32()[0]))throw new System.Exception("Atlas background must be transparent or chroma keyed");
        var json=new StringBuilder("window.HUNTER_FRAMES={\"width\":"+texture.width+",\"height\":"+texture.height+",\"frames\":[");
        for(int row=0;row<4;row++)for(int column=0;column<6;column++)
        {
            var frame=HunterAtlas.Frame(row,column);
            if(frame.pivot.x<0||frame.pivot.x>frame.rect.width||frame.pivot.y<0||frame.pivot.y>frame.rect.height)throw new System.Exception("Invalid motion foot anchor");
            if(row+column>0)json.Append(',');
            json.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{{\"row\":{0},\"column\":{1},\"pivotX\":{2:0.###},\"pivotY\":{3:0.###}}}",row,column,frame.pivot.x,frame.pivot.y);
        }
        json.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"],\"scale\":{0:0.######}}};",HunterAtlas.Scale);
        json.Append("\nwindow.HUNTER_ATLAS=\"data:image/png;base64,").Append(System.Convert.ToBase64String(File.ReadAllBytes("Assets/Resources/Motion/HunterSkills.png"))).Append("\";");
        Directory.CreateDirectory("Previews");File.WriteAllText("Previews/HunterMotionMetadata.js",json.ToString());
        File.Copy("Assets/Resources/Motion/HunterSkills.png","Previews/HunterMotionAtlas.png",true);
        if(ShaderUtil.ShaderHasError(HunterAtlas.Material.shader))throw new System.Exception("Cartoon sprite shader failed compilation");
        Debug.Log("WNN_MOTION_ASSET_CHECKS_PASSED: 24 cells / shared scale / valid foot anchors / bilinear sprite shader");
        ValidateLocomotion();
    }
    static void ValidateLocomotion()
    {
        var texture=Resources.Load<Texture2D>("Motion/HunterLocomotion");
        if(!texture||!texture.isReadable||texture.width%8!=0||texture.height%4!=0)throw new System.Exception("Locomotion atlas must have 8 x 4 readable cells");
        var pixels=texture.GetPixels32();int w=texture.width/8,h=texture.height/4;
        for(int row=0;row<4;row++)for(int col=0;col<8;col++)
        {
            int ink=0;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(HunterAtlas.IsInk(pixels[((3-row)*h+y)*texture.width+col*w+x]))ink++;
            if(ink<w*h*.04f||ink>w*h*.85f)throw new System.Exception("Invalid locomotion cell "+row+"/"+col);
            if(HunterLocomotionAtlas.Frame(row,col).pivot.y!=HunterLocomotionAtlas.Frame(row,0).pivot.y)throw new System.Exception("Gait ground baseline drift");
            for(int x=0;x<w;x++)if(HunterAtlas.IsInk(pixels[((3-row)*h)*texture.width+col*w+x])||HunterAtlas.IsInk(pixels[((4-row)*h-1)*texture.width+col*w+x]))throw new System.Exception("Locomotion crosses row boundary "+row+"/"+col);
            for(int y=0;y<h;y++)if(HunterAtlas.IsInk(pixels[((3-row)*h+y)*texture.width+col*w])||HunterAtlas.IsInk(pixels[((3-row)*h+y)*texture.width+(col+1)*w-1]))throw new System.Exception("Locomotion crosses column boundary "+row+"/"+col);
        }
        Debug.Log("WNN_LOCOMOTION_ASSET_CHECKS_PASSED: 32 cells / clear cell boundaries / fixed ground baselines / constant scale");
    }
}
