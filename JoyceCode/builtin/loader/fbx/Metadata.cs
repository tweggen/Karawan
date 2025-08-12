using System;
using System.Collections.Generic;
using Silk.NET.Assimp;
using static engine.Logger;

namespace builtin.loader.fbx;

public class Metadata
{
    private SortedDictionary<string, object> _mapMetadata = new();
    private SortedDictionary<string, string> _mapMetadataStrings = new();

    public int GetInteger(string key, int defaultValue = 0) =>
        Convert.ToInt32(GetString(key, $"{defaultValue}"));

    public string GetString(string key, string defaultValue="") => 
        _mapMetadataStrings.ContainsKey(key)?_mapMetadataStrings[key]:defaultValue;
    
    public object Get(string key, object defaultValue=null) => 
        _mapMetadata.ContainsKey(key)?_mapMetadata[key]:defaultValue;

    public void Dump()
    {
        foreach (var kvp in _mapMetadataStrings)
        {
            Trace($"\"{kvp.Key}\": \"{kvp.Value}\"");
        }
    }
    
    public unsafe Metadata(Scene* pScene)
    {
        Silk.NET.Assimp.Metadata* metadata = pScene->MMetaData;
        for (uint i = 0; i < metadata->MNumProperties; i++)
        {
            string strKey = metadata->MKeys[i].ToString();
            string strValue = "(unknown)";
            void* p = metadata->MValues[i].MData;
            object value = default;
            switch (metadata->MValues[i].MType)
            {
                case MetadataType.Bool:
                    value = *(bool*)p;
                    break;

                case MetadataType.Int32:
                    value = *(int*)p;
                    break;

                case MetadataType.Uint64:
                    value = *(ulong*)p;
                    break;

                case MetadataType.Float:
                    value = *(float*)p;
                    break;

                case MetadataType.Double:
                    value = *(double*)p;
                    break;

                case MetadataType.Aistring:
                case MetadataType.Aivector3D:
                case MetadataType.Aimetadata:
                    break;

                case MetadataType.Int64:
                    value = *(long*)p;
                    break;

                case MetadataType.Uint32:
                    value = *(uint*)p;
                    break;

            }

            strValue = value.ToString();
            _mapMetadata[strKey] = value;
            _mapMetadata[strKey] = strValue;
        }
    }
}