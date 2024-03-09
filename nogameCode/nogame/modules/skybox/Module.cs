using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using engine.joyce;

namespace nogame.modules.skybox
{
    public class Module : engine.AModule
    {
        private DefaultEcs.Entity _eSkybox;


        public override void ModuleDeactivate()
        {
            _engine.RemoveModule(this);
            _eSkybox.Dispose();
            base.ModuleDeactivate();
        }


        public override void ModuleActivate()
        {
            base.ModuleActivate();

            _eSkybox = _engine.CreateEntity("Skybox");
             var jMeshSkybox = engine.joyce.mesh.Tools.CreateSkyboxMesh(
                 "skybox",
                 2000f, new Vector2(0f, 0f), new Vector2( 1f, 4f/3f ));
            // var jMeshSkybox = engine.joyce.mesh.Tools.CreateCubeMesh(
            //     10f);
            _eSkybox.Set(new engine.joyce.components.Skybox(1000f, 0x00000001));
            var jMaterialSkybox = new engine.joyce.Material();
            jMaterialSkybox.AlbedoColor = (bool) engine.Props.Get("debug.options.flatshading", false) != true
                ? 0x00000000 : 0xff112233;
            jMaterialSkybox.EmissiveTexture = new engine.joyce.Texture("skybox2noborder.png");
            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(jMaterialSkybox, jMeshSkybox), 5000f);
            _eSkybox.Set(new engine.joyce.components.Instance3(jInstanceDesc));

            _engine.AddModule(this);
        }
    }
}
