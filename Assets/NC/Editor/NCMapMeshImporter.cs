using System;
using UnityEditor;

/// <summary>
/// 맵·가구 모델을 임포트할 때 Read/Write를 켠다.
/// NCNavBaker가 실행 중에 NavMesh를 굽는데, 꺼져 있으면 빌드에서 그 메시가 빠진다.
/// </summary>
public class NCMapMeshImporter : AssetPostprocessor
{
    private static readonly string[] Folders = { "Assets/Map/", "Assets/NC/Resources/Asset/Meshes/" };

    private void OnPreprocessModel()
    {
        foreach (string folder in Folders)
        {
            if (assetPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                ((ModelImporter)assetImporter).isReadable = true;
                return;
            }
        }
    }
}
