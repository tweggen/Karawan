
using System;
using System.Collections;
using System.Collections.Generic;
using DefaultEcs;
using DefaultEcs.Internal;

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
                        AMeshEntry aMeshEntry = LoadMesh(jMesh);
                        aMeshEntries.Add(aMeshEntry);
                        meshResource = new Resource<AMeshEntry>(aMeshEntry);
                        _meshResources.Add(jMesh, meshResource);
                    }
                    meshResource.AddReference();
                }
                for (int i=0; i<value.Materials.Count; ++i)
                {
                    Resource<AMaterialEntry> materialResource;
                    engine.joyce.Material jMaterial = value.Materials[i];
                    if (!_materialResources.TryGetValue(jMaterial, out materialResource))
                    {
                        AMaterialEntry aMaterialEntry = LoadMaterial(jMaterial);
                        aMaterialEntries.Add(aMaterialEntry);
                        materialResource = new Resource<AMaterialEntry>(aMaterialEntry);
                        _materialResources.Add(jMaterial, materialResource);
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
                if (_resources.TryGetValue(info, out Resource resource) && resource.RemoveReference())
                {
                    try
                    {
                        Unload(info, resource.Value);
                    }
                    finally
                    {
                        _resources.Remove(info);
                    }
                }
            }
        }

        protected TResource Load(TInfo info);

        protected void OnResourceLoaded(in Entity entity, TInfo info, TResource resource);

        protected virtual void Unload(TInfo info, TResource resource) => (resource as IDisposable)?.Dispose();

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

            world.ThrowIfNull();

            ComponentPool<Splash.components.PfInstance> singleComponents = ComponentManager<Splash.components.PfInstance>.Get(world.WorldId);
            if (singleComponents != null)
            {
                foreach (Entity entity in singleComponents.GetEntities())
                {
                    OnAdded(entity, singleComponents.Get(entity.EntityId));
                }
            }

            ComponentPool<Splash.components.PfInstance> arrayComponents = ComponentManager<Splash.components.PfInstance>.Get(world.WorldId);
            if (arrayComponents != null)
            {
                foreach (Entity entity in arrayComponents.GetEntities())
                {
                    OnAdded(entity, arrayComponents.Get(entity.EntityId));
                }
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

            foreach (KeyValuePair<TInfo, Resource> pair in _resources)
            {
                Unload(pair.Key, pair.Value.Value);
            }

            _resources.Clear();
        }

        #endregion
    }
}
