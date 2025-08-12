#if false
using Silk.NET.Assimp;

namespace builtin.loader.fbx;

public class AxisInterpreter
{
    
    public unsafe AxisInterpreter(Scene* pScene)
    {
        Metadata* metadata = pScene->MMetaData;
        for (uint i = 0; i < metadata->MNumProperties; i++)
        {
            string key = metadata->MKeys[i].ToString();
            
            string strValue = "(unknown)";
            void* p = metadata->MValues[i].MData;
            switch (metadata->MValues[i].MType)
            {

            }
        }
    }
    
    
    #endif