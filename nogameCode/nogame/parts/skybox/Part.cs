﻿using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace nogame.parts.skybox
{
    public class Part : engine.IPart
    {
        private object _lock = new object();

        private engine.Engine _engine;
        private engine.IScene _scene;

        private DefaultEcs.World _ecsWorld;
        private DefaultEcs.Entity _eSkybox;


        public void PartDeactivate()
        {
            _engine.RemovePart(this);
            lock (_lock)
            {
                _engine = null;
                _scene = null;
            }
        }


        public void PartActivate(
            in engine.Engine engine0, 
            in engine.IScene scene0)
        {
            lock (_lock)
            {
                _engine = engine0;
                _scene = scene0;
                _ecsWorld = _engine.GetEcsWorld();
            }

            {
                _eSkybox = _engine.CreateEntity("Skybox");
                 var jMeshSkybox = engine.joyce.mesh.Tools.CreateSkyboxMesh(
                     2000f, new Vector2(0f, 0f), new Vector2( 1f, 4f/3f ));
                // var jMeshSkybox = engine.joyce.mesh.Tools.CreateCubeMesh(
                //     10f);
                _eSkybox.Set(new engine.joyce.components.Skybox(1000f, 0x00000001));
                var jMaterialSkybox = new engine.joyce.Material();
                jMaterialSkybox.AlbedoColor = engine.GlobalSettings.Get("debug.options.flatshading") != "true"
                    ? 0x00000000 : 0xff112233;
                jMaterialSkybox.EmissiveTexture = new engine.joyce.Texture("skybox2noborder.png");
                engine.joyce.InstanceDesc jInstanceDesc = new();
                jInstanceDesc.Meshes.Add(jMeshSkybox);
                jInstanceDesc.MeshMaterials.Add(0);
                jInstanceDesc.Materials.Add(jMaterialSkybox);
                _eSkybox.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            }

            _engine.AddPart(-1000, scene0, this);
        }
    }
}
