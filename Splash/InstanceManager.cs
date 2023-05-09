
using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace Splash
{
    public class InstanceManager : IDisposable
    {
        #region Types

        private sealed class Resource<ValueType>
        {
            public readonly ValueType Value;

            private int _referencesCount;

            public Resource(ValueType value)
            {
                Value = value;
                _referencesCount = 0;
            }

            public void AddReference() => ++_referencesCount;

            public bool RemoveReference() => --_referencesCount == 0;
        }
        
        #endregion

    #if false
        public readonly struct ResourceEnumerable<TInfo, TResource> : IEnumerable<KeyValuePair<TInfo, TResource>>
        {
            private readonly InstanceManager _manager;

            internal ResourceEnumerable(InstanceManager manager)
            {
                _manager = manager;
            }

            #region IEnumerable

            public ResourceEnumerator<TInfo, TResource> GetEnumerator() => new(_manager);

            IEnumerator<KeyValuePair<TInfo, TResource>> IEnumerable<KeyValuePair<TInfo, TResource>>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }


        public struct ResourceEnumerator<TInfo, TResource> : IEnumerator<KeyValuePair<TInfo, TResource>>
        {
            private readonly KeyValuePair<TInfo, TResource>[] _resources;

            private int _index;

            internal ResourceEnumerator(InstanceManager manager)
            {
                lock (manager._lockObject)
                {
                    _resources = new KeyValuePair<TInfo, TResource>[manager._resources.Count];
                    _index = 0;
                    foreach (KeyValuePair<TInfo, TResource> pair in manager._resources)
                    {
                        _resources[_index++] = new KeyValuePair<TInfo, TResource>(pair.Key, pair.Value.Value);
                    }
                }

                _index = -1;

                Current = default;
            }

            #region IEnumerator

            public KeyValuePair<TInfo, TResource> Current { get; private set; }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (++_index < _resources.Length)
                {
                    Current = _resources[_index];
                    return true;
                }

                Current = default;
                return false;
            }

            void IEnumerator.Reset() => _index = -1;

            #endregion

            #region IDisposable

            public void Dispose()
            { }

            #endregion
        }

        #endregion
#endif

        #region Fields

        private readonly object _lockObject;
        private readonly IThreeD _threeD;
        private readonly Dictionary<engine.joyce.Mesh, Resource<AMeshEntry>> _meshResources;
        private readonly Dictionary<engine.joyce.Material, Resource<AMaterialEntry>> _materialResources;

        #endregion

        #region Properties

#if false
        /// <summary>
        /// Gets all the <typeparamref name="TResource"/> loaded by the current instance and their corresponding <typeparamref name="TInfo"/>.
        /// </summary>
        public ResourceEnumerable Resources => new(this);
#endif
        #endregion

        #region Initialisation

        /// <summary>
        /// Creates an instance of type <see cref="AResourceManager{TInfo, TResource}"/>.
        /// </summary>
        protected InstanceManager(in IThreeD threeD)
        {
            _lockObject = new object();
            _threeD = threeD;
            _meshResources = new Dictionary<engine.joyce.Mesh, Resource<AMeshEntry>>();
            _materialResources = new Dictionary<engine.joyce.Material, Resource<AMaterialEntry>>();
        }

        #endregion

        #region Callbacks

        private void OnAdded(in Entity entity, in Splash.components.PfInstance value) => Add(entity, value);

        private void OnChanged(in Entity entity, in Splash.components.PfInstance oldValue, in Splash.components.PfInstance newValue)
        {
            Add(entity, newValue);
            Remove(oldValue);
        }

        private void OnRemoved(in Entity entity, in Splash.components.PfInstance value) => Remove(value);

        #endregion

        #region Methods

        private AMeshEntry LoadMesh(in engine.joyce.Mesh jMesh)
        {
            return _threeD.CreateMeshEntry(jMesh);
        }

        private AMaterialEntry LoadMaterial(in engine.joyce.Material jMaterial)
        {
            return _threeD.CreateMaterialEntry(jMaterial);
        }

        private void Add(in Entity entity, in Splash.components.PfInstance value)
        {
            IList<AMeshEntry> aMeshEntries = new List<AMeshEntry>();
            IList<AMaterialEntry> aMaterialEntries = new List<AMaterialEntry>();

            lock (_lockObject)
            {
                for (int i=0; i<value.Meshes.Count; ++i)
                {
                    Resource<AMeshEntry> meshResource;
                    engine.joyce.Mesh jMesh = value.Meshes[i];
                    if (!_meshResources.TryGetValue(jMesh, out meshResource))
                    {
                        try
                        {
                            AMeshEntry aMeshEntry = LoadMesh(jMesh);
                            aMeshEntries.Add(aMeshEntry);
                            meshResource = new Resource<AMeshEntry>(aMeshEntry);
                            _meshResources.Add(jMesh, meshResource);
                        }
                        catch (Exception e)
                        {
                            Error("Exception loading mesh: {e}");
                        }
                    }
                    meshResource.AddReference();
                }
                for (int i=0; i<value.Materials.Count; ++i)
                {
                    Resource<AMaterialEntry> materialResource;
                    engine.joyce.Material jMaterial = value.Materials[i];
                    if (!_materialResources.TryGetValue(jMaterial, out materialResource))
                    {
                        try {
                            AMaterialEntry aMaterialEntry = LoadMaterial(jMaterial);
                            aMaterialEntries.Add(aMaterialEntry);
                            materialResource = new Resource<AMaterialEntry>(aMaterialEntry);
                            _materialResources.Add(jMaterial, materialResource);
                        }
                        catch (Exception e)
                        {
                            Error("Exception loading mesh: {e}");
                        }
                    }
                    materialResource.AddReference();
                }
            }

            // TXWTODO: Looks inefficient
            /*
             * Finally, assign these arrays to the entity.
             */
            entity.Get<Splash.components.PfInstance>().AMaterialEntries = aMaterialEntries;
            entity.Get<Splash.components.PfInstance>().AMeshEntries = aMeshEntries;
        }

        private void Remove(in Splash.components.PfInstance value)
        {
            lock (_lockObject)
            {
                for (int i = 0; i < value.Meshes.Count; ++i)
                {
                    Resource<AMeshEntry> meshResource;
                    engine.joyce.Mesh jMesh = value.Meshes[i];
                    if (!_meshResources.TryGetValue(jMesh, out meshResource))
                    {
                        try
                        {
                            _unloadMesh(jMesh, meshResource);
                        }
                        finally
                        {
                            _meshResources.Remove(jMesh);
                        }
                        
                    }
                }

                for (int i = 0; i < value.Materials.Count; ++i)
                {
                    Resource<AMaterialEntry> materialResource;
                    engine.joyce.Material jMaterial = value.Materials[i];
                    if (!_materialResources.TryGetValue(jMaterial, out materialResource))
                    {
                        try
                        {
                            _unloadMaterial(jMaterial, materialResource);
                        }
                        finally
                        {
                            _materialResources.Remove(jMaterial);
                        }
                    }
                }

            }
        }

        private void _unloadMesh(engine.joyce.Mesh jMesh, Resource<AMeshEntry> meshResource)
        {
            _threeD.DestroyMeshEntry(meshResource.Value);
        }

        private void _unloadMaterial(engine.joyce.Material jMesh, Resource<AMaterialEntry> materialResource)
        {
            _threeD.UnloadMaterialEntry(materialResource.Value);
        }
        

        public IDisposable Manage(World world)
        {
            IEnumerable<IDisposable> GetSubscriptions(World w)
            {
                yield return w.SubscribeEntityComponentAdded<Splash.components.PfInstance>(OnAdded);
                yield return w.SubscribeEntityComponentChanged<Splash.components.PfInstance>(OnChanged);
                yield return w.SubscribeEntityComponentRemoved<Splash.components.PfInstance>(OnRemoved);
                yield return w.SubscribeEntityComponentAdded<Splash.components.PfInstance>(OnAdded);
                yield return w.SubscribeEntityComponentChanged<Splash.components.PfInstance>(OnChanged);
                yield return w.SubscribeEntityComponentRemoved<Splash.components.PfInstance>(OnRemoved);
            }

            if (null == world)
            {
                ErrorThrow("world must not be null.", (m)=>new ArgumentException(m));
            }

            var entities = world.GetEntities().With<Splash.components.PfInstance>().AsEnumerable();
            foreach (DefaultEcs.Entity entity in entities)
            {
                OnAdded(entity, entity.Get<Splash.components.PfInstance>());
            }

            return GetSubscriptions(world).Merge();
        }

        #endregion

        
        #region IDisposable

        /// <summary>
        /// Unloads all loaded resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            foreach (KeyValuePair<engine.joyce.Mesh, Resource<AMeshEntry>> pair in _meshResources)
            {
                _unloadMesh(pair.Key, pair.Value);
            }
            foreach (KeyValuePair<engine.joyce.Material, Resource<AMaterialEntry>> pair in _materialResources)
            {
                _unloadMaterial(pair.Key, pair.Value);
            }

            _meshResources.Clear();
            _materialResources.Clear();
        }

        #endregion
    }
}
