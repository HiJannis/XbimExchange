﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Xbim.CobieExpress;
using Xbim.Common;

namespace XbimExchanger
{
    /// <summary>
    /// Abstract class for mapping between different object models and schemas
    /// </summary>
    /// <typeparam name="TSourceKey">Type of the key in the From object to link mappings</typeparam>
    /// <typeparam name="TSourceObject">Type of the object to map from</typeparam>
    /// <typeparam name="TTargetObject">Type of the object to map to</typeparam>
    /// <typeparam name="TSourceRepository"></typeparam>
    /// <typeparam name="TTargetRepository"></typeparam>
    public abstract class XbimMappings<TSourceRepository, TTargetRepository, TSourceKey, TSourceObject, TTargetObject> : IXbimMappings<TSourceRepository, TTargetRepository> 
    {
        protected ConcurrentDictionary<TSourceKey, TTargetObject> Results = new ConcurrentDictionary<TSourceKey, TTargetObject>();

        protected XbimMappings(XbimExchanger<TSourceRepository, TTargetRepository> exchanger)
        {    
            Exchanger = exchanger;     
        }

        protected XbimMappings()
        {
            
        }
        
        /// <summary>
        /// Returns the IDictionary of all objects that have been mapped in this mapping class
        /// </summary>
        public IDictionary<TSourceKey, TTargetObject> Mappings
        {
            get { return Results; }
        }


        /// <summary>
        /// Creates an instance of toObject, override for special creation situations
        /// </summary>
        /// <returns></returns>
        public abstract TTargetObject CreateTargetObject();
        
        /// <summary>
        /// Gets the ToObject with the specified key
        /// </summary>
        /// <param name="key">The key to look the object up with</param>
        /// <param name="to">the object which is mapped to this key</param>
        /// <returns>false if no object has been added to this mapping</returns>
        public bool GetTargetObject(TSourceKey key, out TTargetObject to)
        {
            return Results.TryGetValue(key, out to);
        }

        /// <summary>
        /// Gets the object with the specified key or creates one if it does not exist 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TTargetObject GetOrCreateTargetObject(TSourceKey key)
        {
            if (typeof (TSourceKey).IsValueType || !Equals(null, key))
            {
                TTargetObject result;
                if (Results.TryGetValue(key, out result))
                    return result;
                result = CreateTargetObject();
                Results.TryAdd(key, result);
                return result;
                //we can't use this function because it has side effects
                //return Results.GetOrAdd(key, CreateTargetObject());
            }
            return CreateTargetObject();
        }

        /// <summary>
        /// Gets the object with the specified key or creates one if it does not exist 
        /// </summary>
        /// <param name="key">Key to be used to search for exsting object</param>
        /// <param name="result">Existing or created object</param>
        /// <returns>True if new object is created, False if existing object is returned as a result</returns>
        public bool GetOrCreateTargetObject(TSourceKey key, out TTargetObject result)
        {
            if (typeof(TSourceKey).IsValueType || !Equals(null, key))
            {
                if (Results.TryGetValue(key, out result))
                    return false;
                Results.TryAdd(key, result = CreateTargetObject());
                return true;
            }
            result = CreateTargetObject();
            return true;
        }

        /// <summary>
        /// Adds a mapping between the two object all mapped properties are mapped over by the Mapping function
        /// </summary>
        /// <param name="source">The object to map data from</param>
        /// <param name="target">The object to map data to</param>
        /// <returns>Returns the object which has been added to the mapping</returns>
        public TTargetObject AddMapping(TSourceObject source, TTargetObject target)
        {
            var res = Mapping(source, target); 
            return res;
        }

        /// <summary>
        /// Overrident in the concrete class to perform the actual mapping
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns>the mapped object</returns>
        protected abstract TTargetObject Mapping(TSourceObject source, TTargetObject  target );

        public Type MapFromType
        {
            get { return typeof (TSourceObject); }
        }

        public Type MapToType
        {
            get { return typeof(TTargetObject); }
        }

        public Type MapKeyType
        {
            get { return typeof(TSourceKey); }
        }

        public XbimExchanger<TSourceRepository, TTargetRepository> Exchanger { get; set; }

  

        IDictionary<object, object> IXbimMappings<TSourceRepository, TTargetRepository>.Mappings
        {
            get { return Results as IDictionary<object, object>; }
        }

        object IXbimMappings<TSourceRepository, TTargetRepository>.CreateTargetObject()
        {
            return CreateTargetObject();
        }

        bool IXbimMappings<TSourceRepository, TTargetRepository>.GetTargetObject(object key, out object targetObject)
        {
            targetObject = default(TTargetObject);
            TTargetObject target = (TTargetObject)targetObject;
            var result =  GetTargetObject((TSourceKey)key, out target);
            targetObject = target;
            return result;
        }

        object IXbimMappings<TSourceRepository, TTargetRepository>.GetOrCreateTargetObject(object key)
        {
            return GetOrCreateTargetObject((TSourceKey)key);
        }

        object IXbimMappings<TSourceRepository, TTargetRepository>.AddMapping(object source, object target)
        {
            return AddMapping((TSourceObject)source, (TTargetObject)target);
        }

        protected string FirstNonEmptyString(params string[] values) => FirstNonEmptyString(values as IEnumerable<string>);

        protected string FirstNonEmptyString(IEnumerable<string> potentialValues)
        {
            foreach (var val in potentialValues)
            {
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }

            return null;
        }
    }

    public static class CollectionExtension
    {
        public static void AddIfNotPresent(this IOptionalItemSet<CobieCategory> collection, CobieCategory item)
        {
            foreach (var cat in collection)
            {
                if (cat.Value == item.Value)
                {
                    return;
                }
            }

            collection.Add(item);
        }
    }
}
