using Shaman.Dom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shaman.Runtime.Serialization
{
    [RestrictedAccess]
    public class BinSerCommon
    {
        [StaticFieldCategory(StaticFieldCategory.Cache)]
        private static ConcurrentDictionary<Type, List<FieldInfo>> fieldsDict = new ConcurrentDictionary<Type, List<FieldInfo>>();

        internal static List<FieldInfo> GetFields(Type type)
        {
            var q = fieldsDict.TryGetValue(type);
            if (q != null) return q;
            q = type.RecursiveEnumeration(x => x.BaseType()).SelectMany(x =>
             {
                 IEnumerable<FieldInfo> p = x.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                 if (x == typeof(Exception))
                 {
                     p = p.Where(y =>
                         y.Name != "_ipForWatsonBuckets" &&
                         y.Name != "_safeSerializationManager" &&
                         y.Name != "_watsonBuckets" &&
                         y.Name != "_xptrs" &&
                         y.Name != "native_trace_ips" && // mono
                         y.Name != "trace_ips" // mono
                     );
                 }
                 if (x == typeof(System.Runtime.ExceptionServices.ExceptionDispatchInfo))
                 {
                     p = p.Where(y =>
                         y.Name != "m_dynamicMethods" &&
                         y.Name != "m_IPForWatsonBuckets" &&
                         y.Name != "m_WatsonBuckets"
                     );
                 }
#if !STANDALONE
                 if (x == typeof(System.Net.WebException)
#if !NATIVE_HTTP
                 || x == typeof(System.Net.Reimpl.WebException)
#endif
                 )
                 {
                     p = p.Where(y =>
                        y.Name != "m_Response" &&
                        y.Name != "response" // mono
                     );
                 }
#endif
                 if (x == typeof(HtmlNode))
                 {
                     p = p.OrderByDescending(y => y.FieldType.IsArray);
                 }
                 if (x == typeof(HtmlDocument))
                 {
                     p = p.Where(y => y.Name != "_declaredencoding");
                 }
                 return p;
             })
             .Where(x => x.GetCustomAttribute<BinSerIgnoreAttribute>() == null)
             .OrderBy(x => x.Name)
             .ToList();
            fieldsDict.TryAdd(type, q);
            return q;
        }
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Assembly mscorlib = typeof(Type).Assembly();
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Type RuntimeTypeType =
            mscorlib.GetType("System.RuntimeType", false, false) ??
            mscorlib.GetType("System.MonoType");
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Type RuntimeAssemblyType =
            mscorlib.GetType("System.Reflection.RuntimeAssembly", false, false) ??
            mscorlib.GetType("System.Reflection.MonoAssembly");
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Type RuntimeMethodInfoType =
            mscorlib.GetType("System.Reflection.RuntimeMethodInfo", false, false) ??
            mscorlib.GetType("System.Reflection.MonoMethod");
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Type RuntimeFieldInfoType =
            mscorlib.GetType("System.Reflection.RtFieldInfo", false, false) ??
            mscorlib.GetType("System.Reflection.MonoField");
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Type RuntimePropertyInfoType =
            mscorlib.GetType("System.Reflection.RuntimePropertyInfo", false, false) ??
            mscorlib.GetType("System.Reflection.MonoProperty");
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static Type RuntimeEventInfoType =
            mscorlib.GetType("System.Reflection.RuntimeEventInfo", false, false) ??
            mscorlib.GetType("System.Reflection.MonoEvent");
        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal readonly static MethodInfo GetTypeFromHandleMethod =
            typeof(Type).GetMethod("GetTypeFromHandle");


        [StaticFieldCategory(StaticFieldCategory.Stable)]
        private readonly static object TplEtwProviderLog =
            typeof(Type).Assembly().GetType("System.Threading.Tasks.TplEtwProvider")?.GetField("Log", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        [StaticFieldCategory(StaticFieldCategory.Stable)]
        internal static readonly IEnumerable<object> WellKnownObjects = new object[] {
                typeof(string),
                typeof(string).Assembly(),
                RuntimeTypeType,
                RuntimeAssemblyType,
                TplEtwProviderLog,
                System.Threading.Tasks.TaskScheduler.Default,
                CultureInfo.InvariantCulture
        };

        [StaticFieldCategory(StaticFieldCategory.Configuration)]
        internal readonly static Dictionary<Type, Action<BinSerSerializer, object>> customWriters = new Dictionary<Type, Action<BinSerSerializer, object>>();
        [StaticFieldCategory(StaticFieldCategory.Configuration)]
        internal readonly static Dictionary<Type, Func<BinSerDeserializer, Type, object>> customReaders = new Dictionary<Type, Func<BinSerDeserializer, Type, object>>();




        public static void SetUpCustomSerialization<T>(Action<BinSerSerializer, T> write, Func<BinSerDeserializer, T> read, Type runtimeType = null)
        {
            if (runtimeType == null) runtimeType = typeof(T);
            customWriters.Add(runtimeType, (ser, obj) => write(ser, (T)obj));
            customReaders.Add(runtimeType, (des, type) => (T)read(des));
        }
        public static void SetUpCustomSerialization<T>(Action<BinSerSerializer, T> write, Func<BinSerDeserializer, Type, T> read, Type runtimeType)
        {
            customWriters.Add(runtimeType, (ser, obj) => write(ser, (T)obj));
            customReaders.Add(runtimeType, (des, type) => (T)read(des, type));
        }
        public static void SetUpCustomSerialization<T>(Action<BinSerSerializer, T> write, Func<BinSerDeserializer, Type, T> read, Type[] runtimeTypes)
        {
            foreach (var runtimeType in runtimeTypes)
            {
                if (runtimeType != null)
                {
                    customWriters.Add(runtimeType, (ser, obj) => write(ser, (T)obj));
                    customReaders.Add(runtimeType, (des, z) => (T)read(des, z));
                }
            }

        }
        public static void SetUpForbiddenType<T>()
        {
            customWriters.Add(typeof(T), (ser, obj) =>
            {
                Sanity.BreakIfAttached();
                throw new ArgumentException("Cannot serialize type " + typeof(T));
            });
        }

        static BinSerCommon()
        {
            SetUpCustomSerializations();
        }



        private static void SetUpCustomSerializations()
        {


            SetUpCustomSerialization<Assembly>((ser, x) =>
            {
                ser.WriteObjectInternal(x.FullName);
            }, des =>
            {
                var name = des.ReadObjectInternal<string>();
                return AppDomain.CurrentDomain.GetAssemblies().Single(x => x.FullName == name);
            }, RuntimeAssemblyType);

            SetUpCustomSerialization<Type>((ser, x) =>
            {
                ser.WriteObjectInternal(x.Assembly());
                ser.WriteObjectInternal(x.FullName);
            }, des =>
            {
                var asm = des.ReadObjectInternal<Assembly>();
                var name = des.ReadObjectInternal<string>();
                return asm.GetType(name, true, false);
            }, RuntimeTypeType);

            SetUpCustomSerialization<EventInfo>((ser, x) =>
            {
                ser.WriteObjectInternal(x.DeclaringType);
                ser.WriteObjectInternal(x.Name);
            }, des =>
            {
                var t = des.ReadObjectInternal<Type>();
                var name = des.ReadObjectInternal<string>();
                return t.GetEvent(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }, RuntimeEventInfoType);
            SetUpCustomSerialization<MultiValueStringBuilder>((ser, x) =>
            {
                ser.WriteObjectInternal(x.BlockSize, typeof(object));
            }, des =>
            {
                return new MultiValueStringBuilder(des.ReadObjectInternal<int>());
            });
            SetUpCustomSerialization<PropertyInfo>((ser, x) =>
            {
                ser.WriteObjectInternal(x.DeclaringType);
                ser.WriteObjectInternal(x.Name);
            }, des =>
            {
                var t = des.ReadObjectInternal<Type>();
                var name = des.ReadObjectInternal<string>();
                return t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }, RuntimePropertyInfoType);

            SetUpCustomSerialization<FieldInfo>((ser, x) =>
            {
                ser.WriteObjectInternal(x.DeclaringType);
                ser.WriteObjectInternal(x.Name);
            }, des =>
            {
                var t = des.ReadObjectInternal<Type>();
                var name = des.ReadObjectInternal<string>();
                return t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }, RuntimeFieldInfoType);

            var MonoGenericMethodType = Compatibility.IsMono ? typeof(MethodInfo).Assembly().GetType("System.Reflection.MonoGenericMethod") : null;

            SetUpCustomSerialization<MethodInfo>((ser, m) =>
            {
                ser.WriteObjectInternal(m.DeclaringType);
                ser.WriteObjectInternal(m.Name);
                ser.WriteObjectInternal(m.GetParameters().Select(x => x.ParameterType).ToArray());
                ser.WriteObjectInternal(m.ReturnType);

            }, (des, t) =>
        {
            var declaringType = des.ReadObjectInternal<Type>();
            var name = des.ReadObjectInternal<string>();
            var parameters = des.ReadObjectInternal<Type[]>();
            var retval = des.ReadObjectInternal<Type>();
            var method = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Single(x => x.Name == name && x.ReturnType == retval && x.GetParameters().Select(y => y.ParameterType).SequenceEqual(parameters));
            return method;
        }, new[] { RuntimeMethodInfoType, MonoGenericMethodType });

#if !STANDALONE

            SetUpCustomSerialization<EntityType>((ser, x) =>
            {
                if (x.NativeType == null) throw new ArgumentException("Only reflected entity types can be serialized.");
                ser.WriteObjectInternal(x.NativeType);
            }, des =>
            {
                var t = des.ReadObjectInternal<Type>();
                return EntityType.FromNativeType(t);
            });


            SetUpCustomSerialization<Field>((ser, x) =>
            {
                ser.WriteObjectInternal(x.DeclaringType);
                ser.WriteObjectInternal(x.Name);
            }, des =>
            {
                var t = des.ReadObjectInternal<EntityType>();
                var name = des.ReadObjectInternal<string>();
                return t.GetFieldByName(name);
            });
#endif

            SetUpCustomSerialization<CultureInfo>((ser, x) =>
            {
                ser.WriteObjectInternal(x.Name);
            }, des =>
            {
                var name = des.ReadObjectInternal<string>();
#if CORECLR
                return CultureInfoEx.GetCultureInfo(name);
#else
                return CultureInfo.GetCultureInfo(name);
#endif
            });

            SetUpCustomSerialization<CompareInfo>((ser, x) =>
            {
                ser.WriteObjectInternal(x.Name);
            }, des =>
            {
                var name = des.ReadObjectInternal<string>();
                return CompareInfo.GetCompareInfo(name);
            });




            SetUpCustomSerialization<Uri>((ser, x) =>
            {
                ser.WriteObjectInternal(x.AbsoluteUri);
            }, des =>
            {
#if STANDALONE
                return new Uri(des.ReadObjectInternal<string>());
#else
                return des.ReadObjectInternal<string>().AsUri();
#endif
            });

            SetUpCustomSerialization<System.Collections.IList>((ser, x) =>
            {
                var elemType = x.GetType().GetGenericArguments()[0];
                ser.WriteObjectInternal(x.Count, typeof(int));
                foreach (var item in x)
                {
                    ser.WriteObjectInternal(item, elemType);
                }
            }, (des, type) =>
            {
                var elemType = type.GetGenericArguments()[0];

                var count = des.ReadObjectInternal<int>();
                var list = (System.Collections.IList)Activator.CreateInstance(type, new object[] { count });
                for (int i = 0; i < count; i++)
                {
                    list.Add(des.ReadObjectInternal(elemType));
                }
                return list;
            }, typeof(List<>));



            SetUpCustomSerialization<System.Collections.IDictionary>((ser, x) =>
            {
                var gen = x.GetType().GetGenericArguments();
                var keyType = gen[0];
                var valueType = gen[1];
                ser.WriteObjectInternal(x.Count, typeof(int));

                var comparer = x.GetType().GetProperty("Comparer").GetValue(x);
                var defComparer = typeof(EqualityComparer<>).MakeGenericTypeFast(keyType).GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                ser.WriteObjectInternal(comparer == defComparer ? null : comparer);

                foreach (System.Collections.DictionaryEntry item in x)
                {
                    ser.WriteObjectInternal(item.Key, keyType);
                    ser.WriteObjectInternal(item.Value, valueType);
                }
            }, (des, type) =>
            {
                var gen = type.GetGenericArguments();
                var keyType = gen[0];
                var valueType = gen[1];

                var count = des.ReadObjectInternal<int>();
                var comparer = des.ReadObjectInternal<object>();
                var dict = (System.Collections.IDictionary)Activator.CreateInstance(type, new object[] { count, comparer });
                for (int i = 0; i < count; i++)
                {
                    var key = des.ReadObjectInternal(keyType);
                    var value = des.ReadObjectInternal(valueType);
                    dict.Add(key, value);
                }
                return dict;
            }, typeof(Dictionary<,>));






            SetUpCustomSerialization<System.Collections.IEnumerable>((ser, x) =>
            {
                var elemType = x.GetType().GetGenericArguments()[0];
                var count = (int)x.GetType().GetProperty("Count").GetValue(x);
                ser.WriteObjectInternal(count, typeof(int));

                var comparer = x.GetType().GetProperty("Comparer").GetValue(x);
                var defComparer = typeof(EqualityComparer<>).MakeGenericTypeFast(elemType).GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                ser.WriteObjectInternal(comparer == defComparer ? null : comparer);

                foreach (var item in x)
                {
                    ser.WriteObjectInternal(item, elemType);
                }
            }, (des, type) =>
            {

                var elemType = type.GetGenericArguments()[0];

                var count = des.ReadObjectInternal<int>();
                var comparer = des.ReadObjectInternal<object>();
                var add = count != 0 ? type.GetMethod("Add") : null;
                var set = Activator.CreateInstance(type, comparer != null ? new object[] { comparer } : null);
                for (int i = 0; i < count; i++)
                {
                    var item = des.ReadObjectInternal(elemType);
                    add.Invoke(set, new[] { item });
                }
                return (System.Collections.IEnumerable)set;
            }, typeof(HashSet<>));



            var CancellationTokenSource_timer = typeof(CancellationTokenSource).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(x => x.Name == "timer" || x.Name == "m_timer");
            /*var Timer_due_time_ms = typeof(System.Threading.Timer).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(x => x.Name == "due_time_ms");
            var Timer_m_timer = typeof(System.Threading.Timer).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(x => x.Name == "m_timer");
            FieldInfo TimerHolder_h_holder;
            FieldInfo TimerQueueTimer_m_dueTime;
            if (Timer_m_timer != null) {
                TimerHolder_h_holder = Timer_m_timer.FieldType.
            }*/

#if !STANDALONE
            SetUpCustomSerialization<SingleThreadSynchronizationContext>((a, b) =>
            {
                Sanity.ShouldntHaveHappened();
            }, b => null);
#endif
            SetUpCustomSerialization<SynchronizationContext>((a, b) =>
            {
                Sanity.ShouldntHaveHappened();
            }, b => null);
            SetUpCustomSerialization<CancellationTokenSource>((ser, x) =>
            {
                var timer = CancellationTokenSource_timer.GetValue(x);
                var cancRequested = x.IsCancellationRequested;

                ser.WriteObjectInternal(timer != null ? true : cancRequested, typeof(bool));
                if (!cancRequested)
                {
                    if (Compatibility.IsMono)
                    {
                        var callbacks = GetFieldValue<ConcurrentDictionary<CancellationTokenRegistration, Action>>(x, "callbacks");
                        ser.WriteObjectInternal(callbacks != null && callbacks.Count > 0 ? callbacks.Select(y => y.Value).ToList() : null);
                    }
                    else
                    {
                        var callbacks = GetFieldValue<Array>(x, "m_registeredCallbacksLists");
                        List<Action> l = null;
                        if (callbacks != null)
                        {
                            l = new List<Action>();
                            foreach (var sparse in callbacks)
                            {
                                if (sparse != null)
                                {
                                    var head = GetFieldValue(sparse, "m_head");
                                    foreach (var k in head.RecursiveEnumeration(y => GetFieldValue(y, "m_next")))
                                    {


                                        var elements = GetFieldValue<Array>(k, "m_elements");
                                        if (elements != null)
                                        {
                                            foreach (var el in elements)
                                            {
                                                if (el != null)
                                                {
                                                    var state = GetFieldValue(el, "StateForCallback");
                                                    var callback = GetFieldValue<Action<object>>(el, "Callback");
                                                    var syncCtx = GetFieldValue<SynchronizationContext>(el, "TargetSyncContext");
                                                    l.Add(MakeDelegate(callback, state, syncCtx));
                                                }

                                            }

                                        }
                                    }
                                }

                            }
                        }
                        ser.WriteObjectInternal(l != null && l.Count != 0 ? l : null);
                    }
                }
                else
                {
                    ser.WriteObjectInternal<object>(null);
                }
            }, des =>
            {
                var cancelled = des.ReadObjectInternal<bool>();
                var registrations = des.ReadObjectInternal<List<Action>>();
                var k = new CancellationTokenSource();
                if (cancelled) k.Cancel();
                if (registrations != null)
                {
                    foreach (var item in registrations)
                    {
                        item();
                    }
                }
                return k;
            });
#if !STANDALONE
            SetUpCustomSerialization<EntitySet>((ser, x) =>
            {
                ser.WriteObjectInternal(x.Url.AbsoluteUri);
            }, des =>
            {
                return (EntitySet)AwdeeUrlParser.Parse(des.ReadObjectInternal<string>().AsUri());
            }, typeof(EntitySet<>));

            SetUpForbiddenType<Shaman.Runtime.DetailSource>();
#endif
        }

        [Configuration]
        internal static readonly bool Configuration_EmitSerializer;


        private static Action MakeDelegate(Action<object> deleg, object state, SynchronizationContext syncCtx)
        {
            if (syncCtx != null)
            {
                return () =>
                {
#if STANDALONE
                    syncCtx.Post(s => deleg(s), state);
#else
                    syncCtx.Post(() =>
                    {
                        deleg(state);
                    });
#endif
                };
            }
            else
            {
                return () => deleg(state);

            }
        }

        private static FieldInfo GetFieldByName(Type type, string name, string monoName = null)
        {
            if (Compatibility.IsMono) return type.GetField(monoName ?? name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static T GetFieldValue<T>(object obj, string name, string monoName = null)
        {
            return (T)(GetFieldByName(obj.GetType(), name, monoName).GetValue(obj));
        }

        private static object GetFieldValue(object obj, string name, string monoName = null)
        {
            return (GetFieldByName(obj.GetType(), name, monoName).GetValue(obj));
        }


    }
}
